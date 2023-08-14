using System;
using System.Globalization;

namespace Impostors.Editor
{
    public static class UnityString
    {
        public static string Format(string fmt, params object[] args)
        {
            return string.Format((IFormatProvider) CultureInfo.InvariantCulture.NumberFormat, fmt, args);
        }
    }
}