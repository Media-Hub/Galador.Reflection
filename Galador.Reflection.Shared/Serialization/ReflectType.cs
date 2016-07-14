﻿using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.IO;
using System.Runtime.CompilerServices;

namespace Galador.Reflection.Serialization
{

    /// <summary>
    /// The well known collection type implemented by the type. It is not a flag enum. Only one at a time is supported
    /// </summary>
    public enum ReflectCollectionType : byte
    {
        /// <summary>
        /// This type implement no interface
        /// </summary>
        None,
        /// <summary>
        /// This type implement <see cref="IList"/>
        /// </summary>
        IList,
        /// <summary>
        /// This type implement <see cref="ICollection{T}"/>
        /// </summary>
        ICollectionT,
        /// <summary>
        /// This type implement <see cref="IDictionary"/>
        /// </summary>
        IDictionary,
        /// <summary>
        /// This type implement <see cref="IDictionary{TKey, TValue}"/>
        /// </summary>
        IDictionaryKV,
    }

    // TODO add support for DataContract, DataMember, IgnoreMember

    /// <summary>
    /// The type representing locally all relevant serialization info of a .NET type.
    /// One can object local information with <see cref="ReflectType.GetType(Type)"/>.
    /// This information will be serialized along with object data to exactly reproduce object format when deserializing.
    /// </summary>
    [SerializationSettings(false)]
    public sealed partial class ReflectType
    {
        #region GetType()

        /// <summary>
        /// Gets the <see cref="ReflectType"/> information of an object
        /// </summary>
        public static ReflectType GetType(object o)
        {
            if (o == null)
                return RObject;
            if (o is Type)
                return RType;
            if (o is ReflectType)
                return RReflectType;
            if (o is Missing)
                return ((Missing)o).Type;
            return GetType(o.GetType());
        }

        /// <summary>
        /// Gets the <see cref="ReflectType"/> information of a type
        /// </summary>
        public static ReflectType GetType(Type type)
        {
            if (type == null)
                return null;
            lock (sReflectCache)
            {
                ReflectType result;
                if (!sReflectCache.TryGetValue(type, out result))
                {
                    result = new ReflectType();
                    sReflectCache[type] = result;
                    result.Initialize(type);
                }
                return result;
            }
        }
        static Dictionary<Type, ReflectType> sReflectCache = new Dictionary<System.Type, ReflectType>();

        #endregion

        // this must be first
        static readonly SerializationSettingsAttribute DefaultSettings = new SerializationSettingsAttribute();

        internal static readonly Assembly MSCORLIB = typeof(object).GetTypeInfo().Assembly;
        internal bool IsMscorlib() { return Type != null && Type.GetTypeInfo().Assembly == MSCORLIB; }

        /// <summary>
        /// The serialization information about <see cref="System.Object"/> type. Preloaded for performance and implementation reason.
        /// </summary>
        public static readonly ReflectType RObject = GetType(typeof(object));
        /// <summary>
        /// The serialization information about <see cref="ReflectType"/> type. Preloaded for performance and implementation reason.
        /// </summary>
        public static readonly ReflectType RReflectType = GetType(typeof(ReflectType));
        /// <summary>
        /// The serialization information about <see cref="System.String"/> type. Preloaded for performance and implementation reason.
        /// </summary>
        public static readonly ReflectType RString = GetType(typeof(string));
        /// <summary>
        /// The serialization information about <see cref="System.Type"/> type. Preloaded for performance and implementation reason.
        /// </summary>
        public static readonly ReflectType RType = GetType(typeof(Type));
        /// <summary>
        /// The serialization information about <see cref="System.Nullable{T}"/> type. Preloaded for performance and implementation reason.
        /// </summary>
        public static readonly ReflectType RNullable = GetType(typeof(Nullable<>));

        // ==== FLAGS =====

        /// <summary>
        /// A quick way to categorize the item in either a well known primitive type (written directly to the <see cref="IPrimitiveWriter"/>)
        /// or more elaborate object needing a description
        /// </summary>
        public PrimitiveType Kind { get; private set; } = PrimitiveType.None;
        /// <summary>
        /// Which, if any, well known collection type is implemented by this class.
        /// </summary>
        public ReflectCollectionType CollectionType { get; private set; } = ReflectCollectionType.None;
        /// <summary>
        /// Whether this represent a pointer type or not.
        /// </summary>
        public bool IsPointer { get; private set; }
        /// <summary>
        /// Whether this represent a pointer type, or not.
        /// </summary>
        public bool IsArray { get; private set; }
        /// <summary>
        /// Whether this represent a generic type, or not.
        /// </summary>
        public bool IsGeneric { get; private set; }
        /// <summary>
        /// Whether this represent a generic type definition, or not.
        /// </summary>
        public bool IsGenericTypeDefinition { get; private set; }
        /// <summary>
        /// Whether this represent a generic type parameter, or not.
        /// </summary>
        public bool IsGenericParameter { get; private set; }
        /// <summary>
        /// Whether this represent an enum, or not.
        /// </summary>
        public bool IsEnum { get; private set; }
        /// <summary>
        /// Whether this represent a nullable type, or not.
        /// </summary>
        public bool IsNullable { get; private set; }
        /// <summary>
        /// Whether this type is supported, or not (i.e. <c>supported = Ignored == false</c>).
        /// </summary>
        public bool IsIgnored { get; private set; }
        /// <summary>
        /// Whether this type can have subclass or not (i..e <c>canHaveSubclass = IsFinal == false</c>).
        /// </summary>
        public bool IsFinal { get; private set; }
        /// <summary>
        /// Whether there is an <see cref="ISurrogate{T}"/> type to use to serialize instances if that type, or not.
        /// </summary>
        public bool HasSurrogate { get; private set; }
        /// <summary>
        /// Whether there is a <see cref="TypeConverter"/> to serialize instance of that type, or not.
        /// </summary>
        public bool HasConverter { get; private set; }
        /// <summary>
        /// Whether this type is <see cref="ISerializable"/> or not.
        /// </summary>
        public bool IsISerializable { get; private set; }
        /// <summary>
        /// Whether this is a reference type, or not.
        /// </summary>
        public bool IsReference { get; private set; }
        /// <summary>
        /// Whether this type is using the default save (i.e. members + collection Type) or not. Only relevant for the serializer itself.
        /// </summary>
        public bool IsDefaultSave { get; private set; }
        /// <summary>
        /// Whether this type is a surrogate for another type
        /// </summary>
        public bool IsSurrogateType { get; set; }

        // ==== OBJECTS (not flags) ====

        /// <summary>
        /// Most .NET type (except generic type, array, generic parameter and primitive type) will be identified 
        /// with their <see cref="TypeName"/> and <see cref="AssemblyName"/>
        /// </summary>
        public string TypeName { get; private set; }
        /// <summary>
        /// Most .NET type (except generic type, array, generic parameter and primitive type) will be identified 
        /// with their <see cref="TypeName"/> and <see cref="AssemblyName"/>
        /// </summary>
        public string AssemblyName { get; private set; }
        /// <summary>
        /// The base type serialization information.
        /// </summary>
        public ReflectType BaseType { get; private set; }
        /// <summary>
        /// For array type, this will be the array rank. Or <c>0</c> otherwise.
        /// </summary>
        public int ArrayRank { get; private set; }
        /// <summary>
        /// For type that are generic parameter, the position / index. Otherwise 0.
        /// </summary>
        public int GenericParameterIndex { get; private set; }
        /// <summary>
        /// The .NET <see cref="System.Type"/> that this <see cref="ReflectType"/> represent, if it can be found.
        /// <see cref="ReflectType"/> created by the <see cref="ObjectReader"/> might have a null value there if the type
        /// can't be found. In which case instance of this type will be deserialized as <see cref="Missing"/>.
        /// </summary>
        public Type Type { get; private set; }
        /// <summary>
        /// Info about the <see cref="ISurrogate{T}"/> type to use to serialize instance of that type, if any.
        /// </summary>
        public ReflectType Surrogate { get; private set; }
        /// <summary>
        /// Element type information for pointers, array, generic type and enum.
        /// </summary>
        public ReflectType Element { get; private set; }
        /// <summary>
        /// If this type is an <see cref="ICollection{T}"/> or and <see cref="IDictionary{TKey, TValue}"/> 
        /// this would be the info about <c>T</c>, or <c>TKey</c>, respectively
        /// </summary>
        public ReflectType Collection1 { get; private set; }
        /// <summary>
        /// If this type is an <see cref="IDictionary{TKey, TValue}"/> this would be the info about <c>TValue</c>.
        /// </summary>
        public ReflectType Collection2 { get; private set; }
        /// <summary>
        /// If this is a generic type, will contain either the type parameter or arguments.
        /// </summary>
        public IReadOnlyList<ReflectType> GenericArguments { get; private set; } = Empty<ReflectType>.Array;
        /// <summary>
        /// For normal type (i.e. all but primitive type, array, pointer, enum) contains the list of known members.
        /// i.e. property of field that are marked for serialization.
        /// </summary>
        public MemberList Members { get; } = new MemberList();

        #region CollectionInterface

        /// <summary>
        /// Return the collection type implemented by this type by examining this type
        /// and its <see cref="BaseType"/> If the type is not a collection return self.
        /// </summary>
        public ReflectType CollectionInterface
        {
            get
            {
                if (lazyCollectionInterface == null)
                {
                    var p = this;
                    while (p != null)
                    {
                        if (p.CollectionType != ReflectCollectionType.None)
                        {
                            lazyCollectionInterface = p;
                            return p;
                        }
                        p = p.BaseType;
                    }
                    lazyCollectionInterface = this;

                }
                return lazyCollectionInterface;
            }
        }
        ReflectType lazyCollectionInterface;

        #endregion

        #region RuntimeMembers

        /// <summary>
        /// Gets the all the <see cref="Members"/> for that type, including the <see cref="BaseType"/>'s <see cref="RuntimeMembers"/>.
        /// </summary>
        public IReadOnlyList<Member> RuntimeMembers
        {
            get
            {
                if (lazyMembers == null)
                {
                    int N = Members.Count;
                    int Start = 0;
                    if (BaseType != null)
                    {
                        N += BaseType.RuntimeMembers.Count;
                        Start += BaseType.RuntimeMembers.Count;
                    }
                    var list = new List<Member>(N);
                    if (BaseType != null)
                        list.AddRange(BaseType.RuntimeMembers);
                    list.AddRange(Members);
                    lazyMembers = list;
                }
                return lazyMembers;
            }
        }
        List<Member> lazyMembers;

        #endregion

        internal MethodInfo listWrite, listRead;

        #region utilities: ParentHierarchy() TryConstruct()

        /// <summary>
        /// Return the <see cref="BaseType"/>, all the BaseType's BaseType recursively.
        /// </summary>
        public IEnumerable<ReflectType> ParentHierarchy()
        {
            var p = BaseType;
            while (p != null)
            {
                yield return p;
                p = p.BaseType;
            }
        }

        void SetConstructor(ConstructorInfo ctor)
        {
            if (ctor == null)
            {
#if __NET__ || __NETCORE__
                if (Type.GetTypeInfo().IsValueType)
                    fastCtor = EmitHelper.CreateParameterlessConstructorHandler(Type);
#endif
                return;
            }

            var ps = ctor.GetParameters();
#if __NET__ || __NETCORE__
            if (ps.Length == 0)
            {
                fastCtor = EmitHelper.CreateParameterlessConstructorHandler(ctor);
                return;
            }
#endif
            var cargs = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                if (!p.HasDefaultValue)
                    return;
                cargs[i] = p.DefaultValue;
            }
            emtpy_constructor = ctor;
            empty_params = cargs;

        }
        void SetConstructor(ReflectType local)
        {
#if __NET__ || __NETCORE__
            fastCtor = local.fastCtor;
#endif
            emtpy_constructor = local.emtpy_constructor;
            empty_params = local.empty_params;
        }

        /// <summary>
        /// Will create and return a new instance of <see cref="Type"/> associated to this <see cref="ReflectType"/>
        /// By using either the default constructor (i.e. constructor with no parameter or where all parameters have 
        /// a default value) or creating a so called "uninitialized object". The later case "might" not work very well...
        /// </summary>
        public object TryConstruct()
        {
            if (Type == null)
                return null;
#if __NET__ || __NETCORE__
            if (fastCtor != null)
                return fastCtor();
#endif
            if (emtpy_constructor != null)
                return emtpy_constructor.Invoke(empty_params);
            return Type.GetUninitializedObject();
        }
        ConstructorInfo emtpy_constructor;
        object[] empty_params;
#if __NET__ || __NETCORE__
        Func<object> fastCtor;
#endif

        #endregion

        #region ctors() Initialize()

        internal ReflectType() { }

        void Initialize(Type type)
        {
            Type = type;
            var ti = type.GetTypeInfo();
            IsFinal = true;
            IsReference = type.GetTypeInfo().IsByRef;
            if (IsTypeIgnored(type))
            {
                IsIgnored = true;
            }
            else if (type.IsArray)
            {
                if (type == typeof(byte[]))
                {
                    Kind = PrimitiveType.Bytes;
                }
                else
                {
                    IsArray = true;
                    ArrayRank = type.GetArrayRank();
                    Element = GetType(type.GetElementType());
                }
            }
            else if (type.IsPointer)
            {
                IsFinal = true;
                IsIgnored = true;
                IsReference = false;
                IsPointer = true;
                Element = GetType(type.GetElementType());
            }
            else if (type.IsGenericParameter)
            {
#if __PCL__
                throw new NotSupportedException("PCL");
#else
                IsGenericParameter = true;
                var pargs = type.DeclaringType.GetTypeInfo().GetGenericArguments();
                for (int i = 0; i < pargs.Length; i++)
                    if (pargs[i] == type)
                    {
                        GenericParameterIndex = i;
                        break;
                    }
#endif
            }
            else
            {
                Kind = KnownTypes.GetKind(type);
                if (Kind == PrimitiveType.String)
                {
                    IsReference = true;
                }
                else if (Kind == PrimitiveType.Object)
                {
                    SetConstructor(Type.TryGetConstructors().OrderBy(x => x.GetParameters().Length).FirstOrDefault());
                    if (ti.IsGenericType)
                    {
                        IsGeneric = true;
                        if (ti.IsGenericTypeDefinition())
                        {
                            IsGenericTypeDefinition = true;
                        }
                        else
                        {
                            Element = GetType(ti.GetGenericTypeDefinition());
                            IsNullable = Element == RNullable;
                        }
#if __PCL__
                            throw new NotSupportedException("PCL");
#else
                        GenericArguments = ti.GetGenericArguments().Select(x => GetType(x)).ToArray();
                        IsIgnored = GenericArguments.Any(x => x.IsIgnored);
#endif
                    }
                    if (!IsGeneric || IsGenericTypeDefinition)
                    {
                        var att = ti.GetCustomAttribute<SerializationNameAttribute>();
                        if (att == null)
                        {
                            TypeName = type.FullName;
                            if (!IsMscorlib())
                                AssemblyName = ti.Assembly.GetName().Name;
                        }
                        else
                        {
                            TypeName = att.TypeName;
                            AssemblyName = att.AssemblyName;
                        }
                        if (TypeName == null)
                        {
                            IsIgnored = true;
                        }
                    }
                    if (ti.IsEnum)
                    {
                        Element = GetType(ti.GetEnumUnderlyingType());
                        IsEnum = true;
                    }
                    else if (typeof(Delegate).IsBaseClass(type) || type == typeof(IntPtr))
                    {
                        IsIgnored = true;
                        IsReference = ti.IsClass;
                    }
                    else
                    {
                        IsReference = ti.IsClass || ti.IsInterface;
                        IsFinal = !ti.IsClass || ti.IsSealed;
                        IsDefaultSave = true;
                        if (IsReference && Type != typeof(object))
                        {
                            BaseType = GetType(ti.BaseType);
                        }
#if !__PCL__
                        IsSurrogateType = Type.GetTypeHierarchy().Any(x => x.GetTypeInfo().IsInterface && x.GetTypeInfo().IsGenericType && x.GetTypeInfo().GetGenericTypeDefinition() == typeof(ISurrogate<>));
#endif
                        Type tSurrogate;
                        if (KnownTypes.TryGetSurrogate(type, out tSurrogate))
                        {
                            Surrogate = GetType(tSurrogate);
                            HasSurrogate = true;
                        }
                        else if (typeof(ISerializable).IsBaseClass(type))
                        {
                            IsISerializable = true;
                        }
                        else if (GetTypeConverter() != null)
                        {
                            HasConverter = true;
                        }
                        else
                        {
                            Predicate<string> skip = s =>
                            {
                                var p = BaseType;
                                while (p != null)
                                {
                                    if (p.Members.ContainsKey(s))
                                        return true;
                                    p = p.BaseType;
                                }
                                return false;
                            };
                            var attr = ti.GetCustomAttribute<SerializationSettingsAttribute>() ?? DefaultSettings;
                            foreach (var pi in ti.DeclaredFields)
                            {
                                if (IsTypeIgnored(pi.FieldType))
                                    continue;
                                if (pi.IsStatic)
                                    continue;
                                if (pi.GetCustomAttribute<NotSerializedAttribute>() != null)
                                    continue;
                                bool inc = pi.IsPublic ? attr.IncludePublicFields : attr.IncludePrivateFields;
                                if (!inc && pi.GetCustomAttribute<SerializedAttribute>() != null)
                                    inc = true;
                                if (!inc)
                                    continue;
                                if (skip(pi.Name))
                                    continue;
                                var pType = GetType(pi.FieldType);
                                var m = new Member
                                {
                                    Name = pi.Name,
                                    Type = pType,
                                };
                                m.SetMember(pi);
                                Members.Add(m);
                            }
                            foreach (var pi in ti.DeclaredProperties)
                            {
                                if (IsTypeIgnored(pi.PropertyType))
                                    continue;
                                if (pi.GetMethod == null || pi.GetMethod.IsStatic || pi.GetMethod.GetParameters().Length != 0)
                                    continue;
                                if (pi.SetMethod == null && pi.PropertyType.GetTypeInfo().IsValueType)
                                    continue;
                                if (pi.GetCustomAttribute<NotSerializedAttribute>() != null)
                                    continue;
                                bool inc = pi.GetMethod.IsPublic ? attr.IncludePublicProperties : attr.IncludePrivateProperties;
                                if (!inc && pi.GetCustomAttribute<SerializedAttribute>() != null)
                                    inc = true;
                                if (!inc)
                                    continue;
                                if (skip(pi.Name))
                                    continue;
                                var pType = GetType(pi.PropertyType);
                                var m = new Member
                                {
                                    Name = pi.Name,
                                    Type = pType,
                                };
                                m.SetMember(pi);
                                Members.Add(m);
                            }
                            Type itype = null;
                            if (ParentHierarchy().All(x => x.CollectionType == ReflectCollectionType.None))
                            {
                                var interfaces = type.GetTypeHierarchy().Where(x => x.GetTypeInfo().IsInterface).Distinct().ToList();
                                itype = interfaces.Where(t => t.GetTypeInfo().IsGenericType && t.GetTypeInfo().GetGenericTypeDefinition() == typeof(IDictionary<,>)).FirstOrDefault();
                                if (itype == null) itype = interfaces.Where(t => t.GetTypeInfo().IsGenericType && t.GetTypeInfo().GetGenericTypeDefinition() == typeof(IDictionary)).FirstOrDefault();
                                if (itype == null) itype = interfaces.Where(t => t.GetTypeInfo().IsGenericType && t.GetTypeInfo().GetGenericTypeDefinition() == typeof(ICollection<>)).FirstOrDefault();
                                if (itype == null) itype = interfaces.Where(t => t.GetTypeInfo().IsGenericType && t.GetTypeInfo().GetGenericTypeDefinition() == typeof(IList)).FirstOrDefault();
                            }
                            if (itype != null)
                            {
                                var iti = itype.GetTypeInfo();
                                if (iti.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                                {
                                    CollectionType = ReflectCollectionType.IDictionaryKV;
                                    if (iti.IsGenericTypeDefinition)
                                    {
                                        Collection1 = GetType(iti.GenericTypeParameters[0]);
                                        Collection2 = GetType(iti.GenericTypeParameters[1]);
                                    }
                                    else
                                    {
                                        Collection1 = GetType(iti.GenericTypeArguments[0]);
                                        Collection2 = GetType(iti.GenericTypeArguments[1]);
                                    }
                                }
                                else if (itype.GetTypeInfo().GetGenericTypeDefinition() == typeof(IDictionary))
                                {
                                    CollectionType = ReflectCollectionType.IDictionary;
                                }
                                else if (itype.GetTypeInfo().GetGenericTypeDefinition() == typeof(ICollection<>))
                                {
                                    CollectionType = ReflectCollectionType.ICollectionT;
                                    if (iti.IsGenericTypeDefinition)
                                    {
                                        Collection1 = GetType(iti.GenericTypeParameters[0]);
                                    }
                                    else
                                    {
                                        Collection1 = GetType(iti.GenericTypeArguments[0]);
                                    }
                                }
                                else
                                {
                                    CollectionType = ReflectCollectionType.IList;
                                }
                            }
                        }
                    }
                }
            }
            InitHashCode();
        }

        static bool IsTypeIgnored(Type type)
        {
#if __PCL__
            throw new NotSupportedException("PCL");
#else
            if (type.IsPointer)
                return true;
            if (typeof(Delegate).IsBaseClass(type) || type == typeof(IntPtr))
                return true;
            var ti = type.GetTypeInfo();
            if (ti.IsGenericParameter || ti.IsGenericTypeDefinition)
                return false;
            if (ti.GetGenericArguments().Any(x => IsTypeIgnored(x)))
                return true;
            if (type.FullName == null) // Last, as GenericParameter has null FullName
                return true;
            return false;
#endif
        }

        #endregion

        #region GetHashCode() Equals() ToString()

        /// <summary>
        /// Determines whether <paramref name="obj"/> is another <see cref="ReflectType"/> representing 
        /// the same .NET <see cref="Type"/>.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            var o = obj as ReflectType;
            if (o == null)
                return false;
            if (Type != null || o.Type != null)
                return Type == o.Type;
            if (Kind != o.Kind)
                return false;
            if (IsArray)
            {
                if (!o.IsArray)
                    return false;
                if (ArrayRank != o.ArrayRank)
                    return false;
                if (!Element.Equals(o.Element))
                    return false;
            }
            else if (IsPointer)
            {
                if (!o.IsPointer)
                    return false;
                if (!Element.Equals(o.Element))
                    return false;
            }
            else if (IsGenericParameter)
            {
                if (!o.IsGenericParameter)
                    return false;
                if (GenericParameterIndex != o.GenericParameterIndex)
                    return false;
            }
            else if (Kind == PrimitiveType.Object)
            {
                if (IsGeneric)
                {
                    if (!o.IsGeneric || IsGenericTypeDefinition != o.IsGenericTypeDefinition)
                        return false;
                    if (!IsGenericTypeDefinition)
                        if (!Element.Equals(o.Element))
                            return false;
                    if (Element.GenericArguments.Count != o.GenericArguments.Count)
                        return false;
                    for (int i = 0; i < GenericArguments.Count; i++)
                        if (!Element.GenericArguments[i].Equals(o.GenericArguments[i]))
                            return false;
                }
                if (!IsGeneric || IsGenericTypeDefinition)
                {
                    if (o.IsGeneric || !o.IsGenericTypeDefinition)
                        return false;
                    if (TypeName != o.TypeName)
                        return false;
                    if (AssemblyName != o.AssemblyName)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        public override int GetHashCode() { return hashcode; }
        int hashcode;
        void InitHashCode()
        {
            hashcode = FlagsToInt();
            if (IsArray)
            {
                hashcode ^= ArrayRank;
                hashcode ^= Element.GetHashCode();
            }
            else if (IsPointer)
            {
                hashcode ^= Element.GetHashCode();
            }
            else if (IsGenericParameter)
            {
                hashcode ^= GenericParameterIndex;
            }
            else if (Kind == PrimitiveType.Object)
            {
                if (IsGeneric)
                {
                    if (!IsGenericTypeDefinition)
                        hashcode ^= Element.GetHashCode();
                    foreach (var item in GenericArguments)
                        hashcode ^= item.GetHashCode();
                }
                if (!IsGeneric || IsGenericTypeDefinition)
                {
                    hashcode ^= (TypeName ?? "").GetHashCode();
                    if (AssemblyName != null)
                        hashcode ^= AssemblyName.GetHashCode();
                }
            }
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance. That can immediately recognisezed
        /// by the astute reader as the Type description.
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            ToString(sb);
            return sb.ToString();
        }
        void ToString(StringBuilder sb)
        {
            string assembly;
            ToString(sb, out assembly);
            if (assembly != null)
                sb.Append(',').Append(assembly);
        }
        void ToString(StringBuilder sb, out string assembly)
        {
            if (IsArray)
            {
                Element.ToString(sb, out assembly);
                sb.Append('[');
                for (int i = 1; i < ArrayRank; i++)
                    sb.Append(',');
                sb.Append(']');
            }
            else if (IsPointer)
            {
                Element.ToString(sb, out assembly);
                sb.Append('*');
            }
            else if (IsGenericParameter)
            {
                sb.Append('T').Append(GenericParameterIndex);
                assembly = null;
            }
            else if (Kind == PrimitiveType.Object)
            {
                if (IsGeneric)
                {
                    if (IsGenericTypeDefinition)
                    {
                        assembly = AssemblyName;
                        sb.Append(TypeName);
                    }
                    else
                    {
                        Element.ToString(sb, out assembly);
                        sb.Append('[');
                        for (int i = 0; i < GenericArguments.Count; i++)
                        {
                            if (i > 0)
                                sb.Append(",");
                            sb.Append('[');
                            GenericArguments[i].ToString(sb);
                            sb.Append(']');
                        }
                        sb.Append(']');
                    }
                }
                else
                {
                    assembly = AssemblyName;
                    sb.Append(TypeName);
                }
            }
            else
            {
                assembly = null;
                switch (Kind)
                {
                    default:
                    case PrimitiveType.None:
                    case PrimitiveType.Object:
                        sb.Append("????");
                        break;
                    case PrimitiveType.String:
                        sb.Append("string");
                        break;
                    case PrimitiveType.Bytes:
                        sb.Append("byte[]");
                        break;
                    case PrimitiveType.Guid:
                        sb.Append("Guid");
                        break;
                    case PrimitiveType.Bool:
                        sb.Append("bool");
                        break;
                    case PrimitiveType.Char:
                        sb.Append("char");
                        break;
                    case PrimitiveType.Byte:
                        sb.Append("byte");
                        break;
                    case PrimitiveType.SByte:
                        sb.Append("sbyte");
                        break;
                    case PrimitiveType.Int16:
                        sb.Append("short");
                        break;
                    case PrimitiveType.UInt16:
                        sb.Append("ushort");
                        break;
                    case PrimitiveType.Int32:
                        sb.Append("int");
                        break;
                    case PrimitiveType.UInt32:
                        sb.Append("uint");
                        break;
                    case PrimitiveType.Int64:
                        sb.Append("long");
                        break;
                    case PrimitiveType.UInt64:
                        sb.Append("ulong");
                        break;
                    case PrimitiveType.Single:
                        sb.Append("float");
                        break;
                    case PrimitiveType.Double:
                        sb.Append("double");
                        break;
                    case PrimitiveType.Decimal:
                        sb.Append("decimal");
                        break;
                }
            }
        }

        #endregion

        #region class Member MemberList

        /// <summary>
        /// Represent a member of this type, i.e. a property or field that will be serialized.
        /// </summary>
        public partial class Member
        {
            /// <summary>
            /// This is the member name for the member, i.e. <see cref="MemberInfo.Name"/>.
            /// </summary>
            public string Name { get; internal set; }

            /// <summary>
            /// This is the info for the declared type of this member, i.e. either of
            /// <see cref="PropertyInfo.PropertyType"/> or <see cref="FieldInfo.FieldType"/>.
            /// </summary>
            public ReflectType Type { get; internal set; }

            MemberInfo member;
            PropertyInfo pInfo;
            FieldInfo fInfo;

            // performance fields, depends on platform
#if __NET__ || __NETCORE__
            Action<object, object> setter;
            Func<object, object> getter;
            bool hasFastSetter;
            Action<object, Guid> setterGuid;
            Action<object, bool> setterBool;
            Action<object, char> setterChar;
            Action<object, byte> setterByte;
            Action<object, sbyte> setterSByte;
            Action<object, short> setterInt16;
            Action<object, ushort> setterUInt16;
            Action<object, int> setterInt32;
            Action<object, uint> setterUInt32;
            Action<object, long> setterInt64;
            Action<object, ulong> setterUInt64;
            Action<object, float> setterSingle;
            Action<object, double> setterDouble;
            Action<object, decimal> setterDecimal;
#endif

            internal MemberInfo GetMember() { return member; }
            internal void SetMember(MemberInfo mi)
            {
                member = mi;
                if (mi is PropertyInfo)
                {
                    pInfo = (PropertyInfo)mi;
#if __NET__ || __NETCORE__
                    getter = EmitHelper.CreatePropertyGetterHandler(pInfo);
                    if (pInfo.SetMethod != null)
                    {
                        setter = EmitHelper.CreatePropertySetterHandler(pInfo);
                        switch (Type.Kind)
                        {
                            case PrimitiveType.Guid:
                                hasFastSetter = true;
                                setterGuid = EmitHelper.CreatePropertySetter<Guid>(pInfo);
                                break;
                            case PrimitiveType.Bool:
                                hasFastSetter = true;
                                setterBool = EmitHelper.CreatePropertySetter<bool>(pInfo);
                                break;
                            case PrimitiveType.Char:
                                hasFastSetter = true;
                                setterChar = EmitHelper.CreatePropertySetter<char>(pInfo);
                                break;
                            case PrimitiveType.Byte:
                                hasFastSetter = true;
                                setterByte = EmitHelper.CreatePropertySetter<byte>(pInfo);
                                break;
                            case PrimitiveType.SByte:
                                hasFastSetter = true;
                                setterSByte = EmitHelper.CreatePropertySetter<sbyte>(pInfo);
                                break;
                            case PrimitiveType.Int16:
                                hasFastSetter = true;
                                setterInt16 = EmitHelper.CreatePropertySetter<short>(pInfo);
                                break;
                            case PrimitiveType.UInt16:
                                hasFastSetter = true;
                                setterUInt16 = EmitHelper.CreatePropertySetter<ushort>(pInfo);
                                break;
                            case PrimitiveType.Int32:
                                hasFastSetter = true;
                                setterInt32 = EmitHelper.CreatePropertySetter<int>(pInfo);
                                break;
                            case PrimitiveType.UInt32:
                                hasFastSetter = true;
                                setterUInt32 = EmitHelper.CreatePropertySetter<uint>(pInfo);
                                break;
                            case PrimitiveType.Int64:
                                hasFastSetter = true;
                                setterInt64 = EmitHelper.CreatePropertySetter<long>(pInfo);
                                break;
                            case PrimitiveType.UInt64:
                                hasFastSetter = true;
                                setterUInt64 = EmitHelper.CreatePropertySetter<ulong>(pInfo);
                                break;
                            case PrimitiveType.Single:
                                hasFastSetter = true;
                                setterSingle = EmitHelper.CreatePropertySetter<float>(pInfo);
                                break;
                            case PrimitiveType.Double:
                                hasFastSetter = true;
                                setterDouble = EmitHelper.CreatePropertySetter<double>(pInfo);
                                break;
                            case PrimitiveType.Decimal:
                                hasFastSetter = true;
                                setterDecimal = EmitHelper.CreatePropertySetter<decimal>(pInfo);
                                break;
                        }
                    }
#endif
                }
                else
                {
                    fInfo = (FieldInfo)mi;
#if __NET__ || __NETCORE__
                    getter = EmitHelper.CreateFieldGetterHandler(fInfo);
                    setter = EmitHelper.CreateFieldSetterHandler(fInfo);
                    switch (Type.Kind)
                    {
                        case PrimitiveType.Guid:
                            hasFastSetter = true;
                            setterGuid = EmitHelper.CreateFieldSetter<Guid>(fInfo);
                            break;
                        case PrimitiveType.Bool:
                            hasFastSetter = true;
                            setterBool = EmitHelper.CreateFieldSetter<bool>(fInfo);
                            break;
                        case PrimitiveType.Char:
                            hasFastSetter = true;
                            setterChar = EmitHelper.CreateFieldSetter<char>(fInfo);
                            break;
                        case PrimitiveType.Byte:
                            hasFastSetter = true;
                            setterByte = EmitHelper.CreateFieldSetter<byte>(fInfo);
                            break;
                        case PrimitiveType.SByte:
                            hasFastSetter = true;
                            setterSByte = EmitHelper.CreateFieldSetter<sbyte>(fInfo);
                            break;
                        case PrimitiveType.Int16:
                            hasFastSetter = true;
                            setterInt16 = EmitHelper.CreateFieldSetter<short>(fInfo);
                            break;
                        case PrimitiveType.UInt16:
                            hasFastSetter = true;
                            setterUInt16 = EmitHelper.CreateFieldSetter<ushort>(fInfo);
                            break;
                        case PrimitiveType.Int32:
                            hasFastSetter = true;
                            setterInt32 = EmitHelper.CreateFieldSetter<int>(fInfo);
                            break;
                        case PrimitiveType.UInt32:
                            hasFastSetter = true;
                            setterUInt32 = EmitHelper.CreateFieldSetter<uint>(fInfo);
                            break;
                        case PrimitiveType.Int64:
                            hasFastSetter = true;
                            setterInt64 = EmitHelper.CreateFieldSetter<long>(fInfo);
                            break;
                        case PrimitiveType.UInt64:
                            hasFastSetter = true;
                            setterUInt64 = EmitHelper.CreateFieldSetter<ulong>(fInfo);
                            break;
                        case PrimitiveType.Single:
                            hasFastSetter = true;
                            setterSingle = EmitHelper.CreateFieldSetter<float>(fInfo);
                            break;
                        case PrimitiveType.Double:
                            hasFastSetter = true;
                            setterDouble = EmitHelper.CreateFieldSetter<double>(fInfo);
                            break;
                        case PrimitiveType.Decimal:
                            hasFastSetter = true;
                            setterDecimal = EmitHelper.CreateFieldSetter<decimal>(fInfo);
                            break;
                    }
#endif
                }
            }
            internal void SetMember(Member other)
            {
                if (other.Type.Type == null || other.Type.Type != Type.Type)
                    return;
                member = other.member;
                pInfo = other.pInfo;
                fInfo = other.fInfo;
#if __NET__ || __NETCORE__
                setter = other.setter;
                getter = other.getter;
                hasFastSetter = other.hasFastSetter;
                switch (Type.Kind)
                {
                    case PrimitiveType.Guid:
                        setterGuid = other.setterGuid;
                        break;
                    case PrimitiveType.Bool:
                        setterBool = other.setterBool;
                        break;
                    case PrimitiveType.Char:
                        setterChar = other.setterChar;
                        break;
                    case PrimitiveType.Byte:
                        setterByte = other.setterByte;
                        break;
                    case PrimitiveType.SByte:
                        setterSByte = other.setterSByte;
                        break;
                    case PrimitiveType.Int16:
                        setterInt16 = other.setterInt16;
                        break;
                    case PrimitiveType.UInt16:
                        setterUInt16 = other.setterUInt16;
                        break;
                    case PrimitiveType.Int32:
                        setterInt32 = other.setterInt32;
                        break;
                    case PrimitiveType.UInt32:
                        setterUInt32 = other.setterUInt32;
                        break;
                    case PrimitiveType.Int64:
                        setterInt64 = other.setterInt64;
                        break;
                    case PrimitiveType.UInt64:
                        setterUInt64 = other.setterUInt64;
                        break;
                    case PrimitiveType.Single:
                        setterSingle = other.setterSingle;
                        break;
                    case PrimitiveType.Double:
                        setterDouble = other.setterDouble;
                        break;
                    case PrimitiveType.Decimal:
                        setterDecimal = other.setterDecimal;
                        break;
                }
#endif
            }


            /// <summary>
            /// Gets the value of this member for the given instance.
            /// </summary>
            /// <param name="instance">The instance from which to take the value.</param>
            /// <returns>The value of the member.</returns>
            public object GetValue(object instance)
            {
                if (instance == null)
                    return null;
#if __NET__ || __NETCORE__
                if (getter != null)
                    return getter(instance);
#else
                if (pInfo != null && pInfo.GetMethod != null)
                    return pInfo.GetValue(instance);
                if (fInfo != null)
                    return fInfo.GetValue(instance);
#endif
                return null;
            }

            /// <summary>
            /// Sets the value of this member (if possible) for the given instance.
            /// </summary>
            /// <param name="instance">The instance on which the member value will be set.</param>
            /// <param name="value">The value that must be set.</param>
            public void SetValue(object instance, object value)
            {
                if (instance == null || !Type.Type.IsInstanceOf(value))
                    return;
#if __NET__ || __NETCORE__
                if (setter != null)
                    setter(instance, value);
#else
                if (pInfo != null && pInfo.SetMethod != null && pInfo.PropertyType.IsInstanceOf(value))
                        pInfo.SetValue(instance, value);
                    else if (fInfo != null && fInfo.FieldType.IsInstanceOf(value))
                        fInfo.SetValue(instance, value);
#endif
            }

            internal void ReadValue(ObjectReader reader, object instance)
            {
#if __NET__ || __NETCORE__
                if (hasFastSetter && instance != null)
                {
                    switch (Type.Kind)
                    {
                        case PrimitiveType.Guid:
                            setterGuid(instance, reader.Reader.ReadGuid());
                            break;
                        case PrimitiveType.Bool:
                            setterBool(instance, reader.Reader.ReadBool());
                            break;
                        case PrimitiveType.Char:
                            setterChar(instance, reader.Reader.ReadChar());
                            break;
                        case PrimitiveType.Byte:
                            setterByte(instance, reader.Reader.ReadByte());
                            break;
                        case PrimitiveType.SByte:
                            setterSByte(instance, reader.Reader.ReadSByte());
                            break;
                        case PrimitiveType.Int16:
                            setterInt16(instance, reader.Reader.ReadInt16());
                            break;
                        case PrimitiveType.UInt16:
                            setterUInt16(instance, reader.Reader.ReadUInt16());
                            break;
                        case PrimitiveType.Int32:
                            setterInt32(instance, reader.Reader.ReadInt32());
                            break;
                        case PrimitiveType.UInt32:
                            setterUInt32(instance, reader.Reader.ReadUInt32());
                            break;
                        case PrimitiveType.Int64:
                            setterInt64(instance, reader.Reader.ReadInt64());
                            break;
                        case PrimitiveType.UInt64:
                            setterUInt64(instance, reader.Reader.ReadUInt64());
                            break;
                        case PrimitiveType.Single:
                            setterSingle(instance, reader.Reader.ReadSingle());
                            break;
                        case PrimitiveType.Double:
                            setterDouble(instance, reader.Reader.ReadDouble());
                            break;
                        case PrimitiveType.Decimal:
                            setterDecimal(instance, reader.Reader.ReadDecimal());
                            break;
                    }
                    return;
                }
#endif
                object org = null;
                if (Type.IsReference)
                    org = GetValue(instance);
                var value = reader.Read(Type, org);
                SetValue(instance, value);
            }
        }

        /// <summary>
        /// Specialized list of <see cref="Member"/>.
        /// </summary>
        public class MemberList : IReadOnlyList<Member>, IReadOnlyDictionary<string, Member>
        {
            List<Member> list = new List<Member>();
            Dictionary<string, Member> dict = new Dictionary<string, Member>();

            internal MemberList()
            {
            }
            internal void Add(Member m)
            {
                if (dict.ContainsKey(m.Name))
                    throw new ArgumentException();
                list.Add(m);
                dict[m.Name] = m;
            }

            /// <summary>
            /// Gets the <see cref="Member"/> with the given name.
            /// </summary>
            /// <param name="name">Name of the member.</param>
            /// <returns>Return the member with name, or null.</returns>
            public Member this[string name]
            {
                get
                {
                    Member m;
                    dict.TryGetValue(name, out m);
                    return m;
                }
            }
            /// <summary>
            /// Gets the <see cref="Member"/> at the specified index.
            /// </summary>
            public Member this[int index] { get { return list[index]; } }
            /// <summary>
            /// Number of <see cref="Member"/>.
            /// </summary>
            public int Count { get { return list.Count; } }
            /// <summary>
            /// All the member's names.
            /// </summary>
            public IEnumerable<string> Keys { get { return dict.Keys; } }
            /// <summary>
            /// All the members
            /// </summary>
            public IEnumerable<Member> Values { get { return list; } }

            /// <summary>
            /// Whether a member with such a name exists.
            /// </summary>
            /// <param name="name">The member's name.</param>
            /// <returns>Whether there is such a member.</returns>
            public bool ContainsKey(string name) { return dict.ContainsKey(name); }
            /// <summary>
            /// Enumerate the members.
            /// </summary>
            public IEnumerator<Member> GetEnumerator() { return list.GetEnumerator(); }
            bool IReadOnlyDictionary<string, Member>.TryGetValue(string key, out Member value) { return dict.TryGetValue(key, out value); }

            IEnumerator<KeyValuePair<string, Member>> IEnumerable<KeyValuePair<string, Member>>.GetEnumerator() { return dict.GetEnumerator(); }
            IEnumerator IEnumerable.GetEnumerator() { return list.GetEnumerator(); }
        }

        #endregion

        #region FlagsToInt() IntToFlags()

        int FlagsToInt()
        {
            int result = ((int)Kind) | ((int)CollectionType << 8);
            if (IsPointer) result |= 1 << 16;
            if (IsArray) result |= 1 << 17;
            if (IsGeneric) result |= 1 << 18;
            if (IsGenericTypeDefinition) result |= 1 << 19;
            if (IsGenericParameter) result |= 1 << 20;
            if (IsEnum) result |= 1 << 21;
            if (IsNullable) result |= 1 << 22;
            if (IsIgnored) result |= 1 << 23;
            if (IsFinal) result |= 1 << 24;
            if (HasSurrogate) result |= 1 << 25;
            if (HasConverter) result |= 1 << 26;
            if (IsISerializable) result |= 1 << 27;
            if (IsReference) result |= 1 << 28;
            if (IsDefaultSave) result |= 1 << 29;
            if (IsSurrogateType) result |= 1 << 30;
            return result;
        }
        void IntToFlags(int flags)
        {
            Kind = (PrimitiveType)(flags & 0xFF);
            CollectionType = (ReflectCollectionType)((flags >> 8) & 0xFF);
            IsPointer = (flags & (1 << 16)) != 0;
            IsArray = (flags & (1 << 17)) != 0;
            IsGeneric = (flags & (1 << 18)) != 0;
            IsGenericTypeDefinition = (flags & (1 << 19)) != 0;
            IsGenericParameter = (flags & (1 << 20)) != 0;
            IsEnum = (flags & (1 << 21)) != 0;
            IsNullable = (flags & (1 << 22)) != 0;
            IsIgnored = (flags & (1 << 23)) != 0;
            IsFinal = (flags & (1 << 24)) != 0;
            HasSurrogate = (flags & (1 << 25)) != 0;
            HasConverter = (flags & (1 << 26)) != 0;
            IsISerializable = (flags & (1 << 27)) != 0;
            IsReference = (flags & (1 << 28)) != 0;
            IsDefaultSave = (flags & (1 << 29)) != 0;
            IsSurrogateType = (flags & (1 << 30)) != 0;
        }

        #endregion

        #region Read() Write()

        internal void Read(ObjectReader reader)
        {
            IntToFlags(reader.Reader.ReadInt32());
            Type = KnownTypes.GetType(Kind);
            if (IsArray)
            {
                ArrayRank = (int)reader.Reader.ReadVInt();
                Element = (ReflectType)reader.Read(RReflectType, null);
                if (Element.Type != null)
                {
                    if (ArrayRank == 1) Type = Element.Type.MakeArrayType();
                    else Type = Element.Type.MakeArrayType(ArrayRank);
                }
            }
            else if (IsPointer)
            {
                Element = (ReflectType)reader.Read(RReflectType, null);
                if (Element.Type != null)
                    Type = Element.Type.MakePointerType();
            }
            else if (IsGenericParameter)
            {
                GenericParameterIndex = (int)reader.Reader.ReadVInt();
            }
            else if (Kind == PrimitiveType.Object)
            {
                if (!IsGeneric || IsGenericTypeDefinition)
                {
                    TypeName = (string)reader.Read(RString, null);
                    AssemblyName = (string)reader.Read(RString, null);
                    Type = KnownTypes.GetType(TypeName, AssemblyName);
                    if (reader.SkipMetaData)
                    {
                        if (Type == null)
                            throw new IOException("No MetaData with skip = true");
                        Initialize(Type);
                        return;
                    }
                }
                if (IsGeneric)
                {
                    if (!IsGenericTypeDefinition)
                    {
                        Element = (ReflectType)reader.Read(RReflectType, null);
                        int NArgs = (int)reader.Reader.ReadVInt();
                        var gArgs = new ReflectType[NArgs];
                        GenericArguments = gArgs;
                        for (int i = 0; i < NArgs; i++)
                            gArgs[i] = (ReflectType)reader.Read(RReflectType, null);
                    }
                }
                if (IsEnum)
                {
                    Element = (ReflectType)reader.Read(RReflectType, null);
                }
                else if (HasSurrogate)
                {
                    Surrogate = (ReflectType)reader.Read(RReflectType, null);
                }
                else if (!IsGeneric || IsGenericTypeDefinition)
                {
                    if (IsReference)
                    {
                        BaseType = (ReflectType)reader.Read(RReflectType, null);
                    }
                    var localType = Type != null ? ReflectType.GetType(Type) : null;
                    int NMembers = (int)reader.Reader.ReadVInt();
                    for (int i = 0; i < NMembers; i++)
                    {
                        var m = new Member();
                        m.Name = (string)reader.Read(RString, null);
                        m.Type = (ReflectType)reader.Read(RReflectType, null);
                        var localM = localType?.Members[m.Name];
                        if (localM != null)
                            m.SetMember(localM);
                        Members.Add(m);
                    }
                    switch (CollectionType)
                    {
                        case ReflectCollectionType.ICollectionT:
                            Collection1 = (ReflectType)reader.Read(RReflectType, null);
                            break;
                        case ReflectCollectionType.IDictionaryKV:
                            Collection1 = (ReflectType)reader.Read(RReflectType, null);
                            Collection2 = (ReflectType)reader.Read(RReflectType, null);
                            break;
                    }
                }
            }
            InitHashCode();
            if (IsGeneric && !IsGenericTypeDefinition)
            {
                try
                {
                    if (Element.Type != null && GenericArguments.All(x => x.Type != null))
                        Type = Element.Type.MakeGenericType(GenericArguments.Select(x => x.Type).ToArray());
                }
                catch (Exception ex) { Logging.TraceKeys.Serialization.Error("Couldn't create concrete type", ex.Message); }

                if (IsDefaultSave)
                {
                    var localType = Type != null ? ReflectType.GetType(Type) : null;
                    foreach (var em in Element.Members)
                    {
                        var m = new Member
                        {
                            Name = em.Name,
                            Type = em.Type,
                        };
                        if (em.Type.IsGenericParameter)
                        {
                            m.Type = GenericArguments[em.Type.GenericParameterIndex];
                        }
                        var localM = localType?.Members[m.Name];
                        if (localM != null)
                            m.SetMember(localM);
                        Members.Add(m);
                    }
                    switch (CollectionType)
                    {
                        case ReflectCollectionType.ICollectionT:
                            Collection1 = Element.Collection1;
                            if (Collection1.IsGenericParameter)
                                Collection1 = GenericArguments[Element.Collection1.GenericParameterIndex];
                            break;
                        case ReflectCollectionType.IDictionaryKV:
                            Collection1 = Element.Collection1;
                            if (Collection1.IsGenericParameter)
                                Collection1 = GenericArguments[Element.Collection1.GenericParameterIndex];
                            Collection2 = Element.Collection2;
                            if (Collection2.IsGenericParameter)
                                Collection2 = GenericArguments[Element.Collection2.GenericParameterIndex];
                            break;
                    }
                }
            }
            if (Kind == PrimitiveType.Object && Type != null)
                SetConstructor(ReflectType.GetType(Type));
        }
        internal void Write(ObjectWriter writer)
        {
            writer.Writer.Write(FlagsToInt());
            if (IsArray)
            {
                writer.Writer.WriteVInt(ArrayRank);
                writer.Write(RReflectType, Element);
            }
            else if (IsPointer)
            {
                writer.Write(RReflectType, Element);
            }
            else if (IsGenericParameter)
            {
                writer.Writer.WriteVInt(GenericParameterIndex);
            }
            else if (Kind == PrimitiveType.Object)
            {
                if (!IsGeneric || IsGenericTypeDefinition)
                {
                    writer.Write(RString, TypeName);
                    writer.Write(RString, AssemblyName);
                    if (writer.SkipMetaData)
                        return;
                }
                if (IsGeneric)
                {
                    if (!IsGenericTypeDefinition)
                    {
                        writer.Write(RReflectType, Element);
                        writer.Writer.WriteVInt(GenericArguments.Count);
                        for (int i = 0; i < GenericArguments.Count; i++)
                            writer.Write(RReflectType, GenericArguments[i]);
                    }
                }
                if (IsEnum)
                {
                    writer.Write(RReflectType, Element);
                }
                else if (HasSurrogate)
                {
                    writer.Write(RReflectType, Surrogate);
                }
                else if (IsDefaultSave && (!IsGeneric || IsGenericTypeDefinition))
                {
                    if (IsReference)
                    {
                        writer.Write(RReflectType, BaseType);
                    }
                    writer.Writer.WriteVInt(Members.Count);
                    for (int i = 0; i < Members.Count; i++)
                    {
                        var m = Members[i];
                        writer.Write(RString, m.Name);
                        writer.Write(RReflectType, m.Type);
                    }
                    switch (CollectionType)
                    {
                        case ReflectCollectionType.ICollectionT:
                            writer.Write(RReflectType, Collection1);
                            break;
                        case ReflectCollectionType.IDictionaryKV:
                            writer.Write(RReflectType, Collection1);
                            writer.Write(RReflectType, Collection2);
                            break;
                    }
                }
            }
        }

        #endregion

        #region GetTypeConverter()

        /// <summary>
        /// Gets the <see cref="TypeConverter"/> for this type, if any.
        /// </summary>
        public TypeConverter GetTypeConverter()
        {
#if __PCL__
                    throw new PlatformNotSupportedException("PCL");
#else
            if (converter != null)
                return converter;
            if (Type == null)
                return null;
            var attr = Type.GetTypeInfo().GetCustomAttribute<TypeConverterAttribute>();
            if (attr == null)
                return null;
            var tc = TypeDescriptor.GetConverter(Type);
            if (tc != null
                && tc.CanConvertFrom(typeof(string))
                && tc.CanConvertTo(typeof(string))
                )
            {
                converter = tc;
                return converter;
            }
            return null;
#endif
        }
#if !__PCL__
        TypeConverter converter;
#endif

        #endregion

        #region surrogate helpers: TryGetSurrogate() TryGetOriginal()

        /// <summary>
        /// Will (try to) get a surrogate for that item. I.e. an appropriate instance of <see cref="ISurrogate{T}"/>
        /// </summary>
        /// <param name="o">The object for which a surrogate must be created.</param>
        /// <param name="result">The resulting surrogate.</param>
        /// <returns>Whether a surrogate has been found</returns>
        /// <exception cref="System.ArgumentException">If the surrogate class exists but can't be made</exception>
        public bool TryGetSurrogate(object o, out object result)
        {
            result = null;
            if (o == null || Surrogate == null || KnownTypes.GetType(o) != Type)
                return false;

            result = Surrogate.Type.TryConstruct();
            var ti = typeof(ISurrogate<>).MakeGenericType(KnownTypes.GetType(o));
            var mi = ti.TryGetMethods(nameof(ISurrogate<int>.Initialize), null, KnownTypes.GetType(o)).FirstOrDefault();
            if (mi == null)
                throw new ArgumentException($"Couldn't Initialize surrogate<{Type.FullName}> for {o}");
            mi.Invoke(result, new[] { o });
            return true;
        }

        /// <summary>
        /// If this object is a <see cref="ISurrogate{T}"/> instance for another type, try to get
        /// the appropriate instance, by calling <see cref="ISurrogate{T}.Instantiate"/>.
        /// </summary>
        /// <param name="o">The object that could be a surrogate.</param>
        /// <param name="result">The result instance.</param>
        /// <returns>Whether this object has been successfully identified as a surrogate, or not.</returns>
        public bool TryGetOriginal(object o, out object result)
        {
            result = null;
            if (Kind != PrimitiveType.Object || Type == null || Surrogate == null || Surrogate.Type == null)
                return false;
            if (o == null)
                return false;

            if (KnownTypes.GetType(o) == Type)
            {
                result = o;
                return true;
            }
            if (KnownTypes.GetType(o) != Surrogate.Type)
                return false;

            var surr = (from t in Surrogate.Type.GetTypeHierarchy()
                        let ti = t.GetTypeInfo()
                        where ti.IsInterface
                        where !ti.IsGenericTypeDefinition
                        where ti.GetGenericTypeDefinition() == typeof(ISurrogate<>)
                        let ts = ti.GenericTypeArguments[0]
                        where Type == ts
                        select t
                        ).FirstOrDefault();
            if (surr != null)
            {
                var mi = surr.GetRuntimeMethod(nameof(ISurrogate<int>.Instantiate), Empty<Type>.Array);
                result = mi.Invoke(o, Empty<object>.Array);
                return true;
            }

            return false;
        }

#endregion
    }
}
