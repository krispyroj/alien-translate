using System;
using AlienTranslate;

internal static class ConversionTests
{
    private static int checks;

    private static void Equal(string expected, string actual, string name)
    {
        checks++;
        if (expected != actual)
            throw new Exception(name + ": expected [" + expected + "] but got [" + actual + "]");
    }

    private static int Main()
    {
        Equal("สวัสดี", Kedmanee.Convert("l;ylfu", true), "English keys to Thai");
        Equal("hello", Kedmanee.Convert("้ำสสน", false), "Thai keys to English");
        Equal("l;ylfu", Kedmanee.Convert("สวัสดี", false), "Thai greeting to English keys");
        Equal("สวัสดี", Kedmanee.FixEnglishCapsLock("L;YLFU"), "English Caps Lock");
        Equal("สวัสดี", Kedmanee.FixThaiCapsLock("ศซํศโ๊"), "Thai Caps Lock");
        Equal("๑๒๓", Kedmanee.Convert("@#$", true), "Shifted symbols");
        Equal("", Kedmanee.Convert("", true), "Empty input");
        Equal("你好 👽\r\n\t", Kedmanee.Convert("你好 👽\r\n\t", true), "Unmapped Unicode and whitespace");
        Equal("สวัสดี\r\nสวัสดี", Kedmanee.Convert("l;ylfu\r\nl;ylfu", true), "Multiline input");
        const string keys = "`1234567890-=qwertyuiop[]\\asdfghjkl;'zxcvbnm,./~!@#$%^&*()_+QWERTYUIOP{}|ASDFGHJKL:\"ZXCVBNM<>?";
        Equal(keys, Kedmanee.Convert(Kedmanee.Convert(keys, true), false), "All physical keys round trip");
        Console.WriteLine("Passed " + checks + " conversion checks.");
        return 0;
    }
}
