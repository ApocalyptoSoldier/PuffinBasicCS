namespace Org.Puffinbasic.Common
{
    using System.Text;

    public static class ISOEncoding
    {
        private static readonly Encoding encoding = Encoding.GetEncoding("ISO-8859-1");

        public static byte[] GetBytes(string str) => encoding.GetBytes(str);

        public static string GetString(byte[] bytes) => encoding.GetString(bytes);
    }
}