﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    public class SerializationSettings
    {
        /// <summary>
        /// Whether the reader contain exhaustive (when <see cref="SkipMetaData"/> is <c>false</c>) or minimal (when <see cref="SkipMetaData"/> is <c>true</c>)
        /// type information. It should match <see cref="ObjectWriter.SkipMetaData"/> (i.e. value set in the writer). 
        /// <br/>
        /// If <see cref="SkipMetaData"/> is <c>true</c> and the type can not be resolved or there is a version mismatch data would be irrecoverably corrupted.
        /// <br/>
        /// It should NOT be used, unless it is used for in process object deep cloning.
        /// </summary>
        public bool SkipMetaData { get; set; } = false;

        public bool IgnoreISerializable { get; set; }

        public bool IgnoreTypeConverter { get; set; }


        internal int ToFlags()
        {
            int result = 0;
            result |= SkipMetaData ? 1 << 0 : 0;
            result |= IgnoreISerializable ? 1 << 1 : 0;
            result |= IgnoreTypeConverter ? 1 << 2 : 0;
            return result;
        }
        internal void FromFlags(int l)
        {
            var flags = (int)l;
            SkipMetaData = (flags & (1 << 0)) != 0;
            IgnoreISerializable = (flags & (1 << 1)) != 0;
            IgnoreTypeConverter = (flags & (1 << 2)) != 0;
        }
    }
}
