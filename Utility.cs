using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace UMC.Proxy
{
    public class Utility : UMC.Data.Utility
    {

        public static String MD5(System.Guid guid)
        {
            using (var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider())
            {
                return Guid(new System.Guid(md5.ComputeHash(guid.ToByteArray())));
            }
        }
        public static String NameValue(NameValueCollection Headers)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < Headers.Count; i++)
            {
                sb.AppendFormat("{0}: {1}", Headers.GetKey(i), Headers.Get(i));
                sb.AppendLine();
            }
            sb.AppendLine();
            return sb.ToString();
        }
        static Web.WebMeta FromValue(int index, String html, out int tagEndIndex)
        {
            tagEndIndex = -1;
            var startIndex = 0;
            while (index > -1)
            {
                index--;
                switch (html[index])
                {
                    case ' ':
                        break;
                    case '<':
                        startIndex = index;
                        break;

                    case '\'':
                    case '"':
                        index = html.LastIndexOf(html[index], index - 1);
                        break;
                }
                if (startIndex > 0)
                {
                    break;
                }
            }
            var start = startIndex + 1;
            var attrStart = -1;
            var attrName = String.Empty;

            var webMeta = new Web.WebMeta();
            var IsFindTag = true;
            while (IsFindTag)
            {
                start++;
                switch (html[start])
                {
                    case '\r':
                    case '\n':
                    case '\t':
                    case ' ':
                        IsFindTag = false;
                        webMeta.Put("tag", html.Substring(startIndex + 1, start - startIndex - 1));
                        break;
                    default:
                        IsFindTag = start < html.Length;
                        break;
                }
            }
            var startValue = -1;
            while (start < html.Length)
            {
                switch (html[start])
                {
                    case '\r':
                    case '\n':
                    case '\t':
                    case ' ':
                        if (startValue == -1)
                        {
                            if (attrStart < start - 1 && attrStart > 0)
                            {
                                attrName = html.Substring(attrStart, start - attrStart);
                                webMeta.Put(attrName.ToLower(), String.Empty);
                            }

                        }
                        else
                        {
                            webMeta.Put(attrName.ToLower(), html.Substring(startValue, start - startValue));
                            startValue = -1;
                        }
                        attrStart = start + 1;
                        break;
                    case '=':
                        if (attrStart < start - 1)
                        {
                            attrName = html.Substring(attrStart, start - attrStart);
                        }
                        attrStart = start + 1;
                        startValue = attrStart;
                        break;

                    case '\'':
                    case '"':

                        var startValueIndex = html.IndexOf(html[start], start + 1);
                        if (startValueIndex > 0)
                        {
                            webMeta.Put(attrName.ToLower(), System.Web.HttpUtility.HtmlDecode(html.Substring(start + 1, startValueIndex - start - 1)));
                        }
                        start = startValueIndex;
                        startValue = -1;
                        attrStart = start + 1;

                        break;
                    case '/':
                        tagEndIndex = start + 1;
                        return webMeta;
                    case '>':
                        tagEndIndex = start;
                        return webMeta;
                    default:
                        break;
                }
                start++;
            }
            return webMeta;
        }
        static String FromValue(String html, int index, bool isForm, out int endIndex)
        {
            var ms = FromValue(index, html, out endIndex);
            if (ms.ContainsKey("disabled"))
            {
                return null;
            }
            switch (ms["tag"])
            {
                case "input":
                    var type = ms["type"];
                    switch (type)
                    {
                        case "radio":
                        case "checkbox":
                            if (!ms.ContainsKey("checked") && isForm)
                            {
                                return null;
                            }
                            return ms["value"] ?? "on";
                    }
                    return ms["value"];

                case "select":

                    var end5 = html.IndexOf("</select>", endIndex);

                    var optionHtml = html.Substring(endIndex + 1, end5 - endIndex - 1);

                    var selectedIndex = optionHtml.IndexOf(" selected", StringComparison.CurrentCultureIgnoreCase);

                    if (selectedIndex == -1)
                    {
                        selectedIndex = optionHtml.IndexOf("option");
                        if (selectedIndex == -1)
                        {
                            return String.Empty;
                        }
                    }
                    int optionEndIndex;
                    var ov = FromValue(selectedIndex, optionHtml, out optionEndIndex);
                    if (ov.ContainsKey("value"))
                    {
                        return ov["value"];
                    }
                    else
                    {
                        if (html[endIndex - 1] == '/')
                        {
                            return String.Empty;
                        }
                        var end9 = optionHtml.IndexOf('<', optionEndIndex);
                        return optionHtml.Substring(optionEndIndex + 1, end9 - optionEndIndex - 1);

                    }

                case "textarea":
                    if (html[endIndex - 1] == '/')
                    {
                        return ms["value"] ?? String.Empty;
                    }
                    var end4 = html.IndexOf('<', endIndex);
                    if (end4 > 0)
                    {

                        return System.Web.HttpUtility.HtmlDecode(html.Substring(endIndex + 1, end4 - endIndex - 1));

                    }
                    break;
            }
            return null;
        }
        public static Web.WebMeta FromValue(String html, bool isKey)
        {

            var webMeta = new System.Collections.Generic.Dictionary<String, List<String>>();
            var nKey = " name=";
            int index = html.IndexOf(nKey);
            while (index > 0)
            {
                var startIndex = index + nKey.Length;
                var start = html[startIndex];
                switch (start)
                {
                    case '\'':
                    case '"':
                        var endIndex = html.IndexOf(start, startIndex + 1);
                        if (endIndex > startIndex)
                        {
                            var name = html.Substring(startIndex + 1, endIndex - startIndex - 1);
                            var value = FromValue(html, index, !isKey, out endIndex);
                            if (value != null)
                            {
                                List<String> vs;
                                if (webMeta.TryGetValue(name, out vs))
                                {
                                    vs.Add(value);
                                }
                                else
                                {
                                    vs = new List<string>();
                                    vs.Add(value);
                                    webMeta[name] = vs;
                                }

                                startIndex = endIndex;
                            }
                            else
                            {

                                startIndex = endIndex;
                            }
                        }
                        break;
                }
                index = html.IndexOf(nKey, startIndex);
            }
            var meta = new Web.WebMeta();
            var em = webMeta.GetEnumerator();
            while (em.MoveNext())
            {
                meta.Put(em.Current.Key, String.Join(",", em.Current.Value.ToArray()));
            }
            return meta;
        }
        public static String FormValue(String html, String name)
        {
            var nKey = " name=";
            int index = html.IndexOf(nKey);
            while (index > 0)
            {
                var startIndex = index + nKey.Length;
                var start = html[startIndex];
                switch (start)
                {
                    case '\'':
                    case '"':
                        var endIndex = html.IndexOf(start, startIndex + 1);
                        if (endIndex > startIndex)
                        {
                            if (String.Equals(name, html.Substring(startIndex + 1, endIndex - startIndex - 1)))
                            {
                                return FromValue(html, index, false, out endIndex);
                            }
                        }
                        break;
                }
                index = html.IndexOf(nKey, startIndex);
            }
            return null;
        }
    }
}
