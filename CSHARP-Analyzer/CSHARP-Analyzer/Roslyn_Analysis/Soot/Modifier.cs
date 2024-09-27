using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Roslyn_Analysis.Soot
{
    public class Modifier
    {
        public static readonly int ABSTRACT = 0x0400;
        public static readonly int FINAL = 0x0010;
        public static readonly int INTERFACE = 0x0200;
        public static readonly int NATIVE = 0x0100;
        public static readonly int PRIVATE = 0x0002;
        public static readonly int PROTECTED = 0x0004;
        public static readonly int PUBLIC = 0x0001;
        public static readonly int STATIC = 0x0008;
        public static readonly int SYNCHRONIZED = 0x0020;
        public static readonly int TRANSIENT = 0x0080; /* VARARGS for methods */
        public static readonly int VOLATILE = 0x0040; /* BRIDGE for methods */
        public static readonly int STRICTFP = 0x0800;
        public static readonly int ANNOTATION = 0x2000;
        public static readonly int ENUM = 0x4000;

        // dex specifific modifiers
        public static readonly int SYNTHETIC = 0x1000;
        public static readonly int CONSTRUCTOR = 0x10000;
        public static readonly int DECLARED_SYNCHRONIZED = 0x20000;
        // add

        // modifier for java 9 modules
        public static readonly int REQUIRES_TRANSITIVE = 0x0020;
        public static readonly int REQUIRES_STATIC = 0x0040;
        public static readonly int REQUIRES_SYNTHETIC = 0x1000;
        public static readonly int REQUIRES_MANDATED = 0x8000;

        private Modifier()
        {
        }

        public static bool isAbstract(int m)
        {
            return (m & ABSTRACT) != 0;
        }

        public static bool isFinal(int m)
        {
            return (m & FINAL) != 0;
        }

        public static bool isInterface(int m)
        {
            return (m & INTERFACE) != 0;
        }

        public static bool isNative(int m)
        {
            return (m & NATIVE) != 0;
        }

        public static bool isPrivate(int m)
        {
            return (m & PRIVATE) != 0;
        }

        public static bool isProtected(int m)
        {
            return (m & PROTECTED) != 0;
        }

        public static bool isPublic(int m)
        {
            return (m & PUBLIC) != 0;
        }

        public static bool isStatic(int m)
        {
            return (m & STATIC) != 0;
        }

        public static bool isSynchronized(int m)
        {
            return (m & SYNCHRONIZED) != 0;
        }

        public static bool isTransient(int m)
        {
            return (m & TRANSIENT) != 0;
        }

        public static bool isVolatile(int m)
        {
            return (m & VOLATILE) != 0;
        }

        public static bool isStrictFP(int m)
        {
            return (m & STRICTFP) != 0;
        }

        public static bool isAnnotation(int m)
        {
            return (m & ANNOTATION) != 0;
        }

        public static bool isEnum(int m)
        {
            return (m & ENUM) != 0;
        }

        public static bool isSynthetic(int m)
        {
            return (m & SYNTHETIC) != 0;
        }

        public static bool isConstructor(int m)
        {
            return (m & CONSTRUCTOR) != 0;
        }

        public static bool isDeclaredSynchronized(int m)
        {
            return (m & DECLARED_SYNCHRONIZED) != 0;
        }

        /**
         * Converts the given modifiers to their string representation, in canonical form.
         *
         * @param m
         *          a modifier set
         * @return a textual representation of the modifiers.
         */
        public static String toString(int m)
        {
            string buffer = "";

            if (isPublic(m))
            {
                buffer += "public ";
            }
            else if (isPrivate(m))
            {
                buffer += "private ";
            }
            else if (isProtected(m))
            {
                buffer += "protected ";
            }

            if (isAbstract(m))
            {
                buffer += "abstract ";
            }

            if (isStatic(m))
            {
                buffer += "static ";
            }

            if (isFinal(m))
            {
                buffer += "final ";
            }

            if (isSynchronized(m))
            {
                buffer += "synchronized ";
            }

            if (isNative(m))
            {
                buffer += "native ";
            }

            if (isTransient(m))
            {
                buffer += "transient ";
            }

            if (isVolatile(m))
            {
                buffer += "volatile ";
            }

            if (isStrictFP(m))
            {
                buffer += "strictfp ";
            }

            if (isAnnotation(m))
            {
                buffer += "annotation ";
            }

            if (isEnum(m))
            {
                buffer += "enum ";
            }

            if (isInterface(m))
            {
                buffer += "interface ";
            }

            return buffer.Substring(0, buffer.Length-1);
        }
    }

}
