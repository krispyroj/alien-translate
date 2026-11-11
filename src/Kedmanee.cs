using System;
using System.Collections.Generic;

namespace AlienTranslate
{
    internal static class Kedmanee
    {
        // Keep the physical key positions aligned across layouts and Shift states.
        private const string EnglishUnshifted = "`1234567890-=" + "qwertyuiop[]\\" + "asdfghjkl;'" + "zxcvbnm,./";
        private const string EnglishShifted = "~!@#$%^&*()_+" + "QWERTYUIOP{}|" + "ASDFGHJKL:\"" + "ZXCVBNM<>?";
        private const string ThaiUnshifted = "_ๅ/-ภถุึคตจขช" + "ๆไำพะัีรนยบลฃ" + "ฟหกดเ้่าสวง" + "ผปแอิืทมใฝ";
        private const string ThaiShifted = "%+๑๒๓๔ู฿๕๖๗๘๙" + "๐\"ฎฑธํ๊ณฯญฐ,ฅ" + "ฤฆฏโฌ็๋ษศซ." + "()ฉฮฺ์?ฒฬฦ";

        private const string EnglishKeys = EnglishUnshifted + EnglishShifted;
        private const string ThaiKeys = ThaiUnshifted + ThaiShifted;

        private static readonly Dictionary<char, char> EnglishToThai = BuildMap(EnglishKeys, ThaiKeys);
        private static readonly Dictionary<char, char> ThaiToEnglish = BuildMap(ThaiKeys, EnglishKeys);
        // Kedmanee inverts the Shift layer. US Caps Lock affects only letters.
        private static readonly Dictionary<char, char> ThaiCapsLock = BuildMap(ThaiShifted + ThaiUnshifted, ThaiKeys);
        private static readonly Dictionary<char, char> EnglishCapsLock = BuildMap(
            EnglishUnshifted.ToUpperInvariant() + EnglishShifted.ToLowerInvariant(), ThaiKeys);

        private static Dictionary<char, char> BuildMap(string source, string target)
        {
            var map = new Dictionary<char, char>(source.Length);
            for (int index = 0; index < source.Length; index++)
                map.Add(source[index], target[index]);
            return map;
        }

        internal static string Convert(string text, bool toThai)
        {
            return Apply(text, toThai ? EnglishToThai : ThaiToEnglish);
        }

        internal static string FixThaiCapsLock(string text)
        {
            return Apply(text, ThaiCapsLock);
        }

        internal static string FixEnglishCapsLock(string text)
        {
            return Apply(text, EnglishCapsLock);
        }

        private static string Apply(string text, Dictionary<char, char> map)
        {
            var output = text.ToCharArray();
            for (int index = 0; index < output.Length; index++)
            {
                char mapped;
                if (map.TryGetValue(output[index], out mapped))
                    output[index] = mapped;
            }
            return new string(output);
        }
    }
}
