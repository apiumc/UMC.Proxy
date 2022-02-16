using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UMC.Proxy
{
    public class SiteConfig
    {
        public enum HostReplaceModel
        {
            Replace = 1,
            Remove = 2,
            Input = 4,
            CDN = 8,
            Script = 16
        }
        public class ReplaceSetting
        {
            public HostReplaceModel Model
            {
                get;
                set;
            }
            public System.Collections.Generic.Dictionary<String, Uri> Hosts
            {
                get;
                set;
            }
        }



        public int WeightTotal
        {
            get;
            private set;
        }
        public int[] Weights
        {
            get;
            private set;
        }
        public string Caption
        {
            get; private set;
        }


        public SiteConfig() { }
        public SiteConfig(Entities.Site site)
        {
            this.Caption = site.Caption;
            var vindex = this.Caption.IndexOf("v.", StringComparison.CurrentCultureIgnoreCase);
            if (vindex > -1)
            {
                this.Caption = this.Caption.Substring(0, vindex);
            }
            if (String.IsNullOrEmpty(site.Home) == false && (site.Home.StartsWith("https:") || site.Home.StartsWith("http:")))
            {
                this.Home = new Uri(site.Home).PathAndQuery;
            }
            else
            {
                this.Home = site.Home;
            }

            this.Root = site.Root;
            this.Site = site;

            var dom = site.Domain.Split(',', '\n');
            List<string> domains = new List<string>();
            var ls = new List<int>();
            var total = 0;
            for (var i = 0; i < dom.Length; i++)
            {
                var v = dom[i].Trim();
                if (String.IsNullOrEmpty(v) == false)
                {
                    if (v.EndsWith("]"))
                    {
                        var vin = v.LastIndexOf("[");
                        var tUrl = v;
                        if (vin > -1)
                        {
                            ls.Add(UMC.Data.Utility.IntParse(v.Substring(vin + 1).Trim(']', '[').Trim(), 1));

                            total += ls[ls.Count - 1];
                            tUrl = v.Substring(0, vin).TrimEnd(']', ' ').Trim();
                        }
                        else
                        {
                            total++;
                            ls.Add(1);
                        }

                        var sIndex = tUrl.LastIndexOf('/');
                        if (sIndex > 0)
                        {
                            tUrl = tUrl.Substring(0, sIndex);
                        }
                        domains.Add(tUrl);
                    }
                    else
                    {
                        var tIndex = v.IndexOf('@');
                        var tUrl = v;
                        if (tIndex > 0)
                        {
                            var uvs = v.Substring(tIndex + 1).Split(',', ' ');
                            var tUsers = new List<String>();
                            foreach (var uv in uvs)
                            {
                                if (String.IsNullOrEmpty(uv.Trim()) == false)
                                {
                                    tUsers.Add(uv.Trim());
                                }
                            }
                            if (tUsers.Count > 0)
                            {
                                var sIndex = tUrl.LastIndexOf('/');
                                if (sIndex > 0)
                                {
                                    tUrl = tUrl.Substring(0, sIndex);
                                }
                                this._test[tUrl.Trim()] = tUsers.ToArray();
                            }
                        }
                        else
                        {
                            var sIndex = tUrl.LastIndexOf('/');
                            if (sIndex > 0)
                            {
                                tUrl = tUrl.Substring(0, sIndex);
                            }
                            domains.Add(tUrl);
                            total++;
                            ls.Add(1);
                        }
                    }
                }
            }
            this.Domains = domains.ToArray();

            this.WeightTotal = total;
            this.Weights = ls.ToArray();

            this.AllowPath = Config(site.AuthConf);
            this.OutputCookies = Config(site.OutputCookies);
            this.LogoutPath = Config(site.LogoutPath);
            this.AppendJSConf = Config(site.AppendJSConf);
            this.AdminConf = Config(site.AdminConf);
            this.LogPathConf = Config(site.LogPathConf);

            if (String.IsNullOrEmpty(site.Conf) == false)
            {
                var v = UMC.Data.JSON.Deserialize(site.Conf) as Hashtable;
                if (v != null)
                {
                    var pem = v.GetEnumerator();
                    while (pem.MoveNext())
                    {
                        var key = pem.Key as string;
                        _subSite.Add(key, pem.Value.ToString());
                    }
                }
            }
            InitStatic(site.StaticConf);
            InitHost(site.HostReConf);
            InitHeader(site.HeaderConf);


            this.AllowAllPath = this.AllowPath.Contains("*");
        }
        public bool AllowAllPath
        {
            get; set;
        }
        public static bool CheckMime(Hashtable login)
        {
            var rawUrl = login["RawUrl"] as string;
            if (String.IsNullOrEmpty(rawUrl))
            {
                return false;

            }


            var Method = login["Method"] as string;
            if (String.IsNullOrEmpty(Method))
            {
                return false;
            }

            switch (Method)
            {
                case "POST":
                case "PUT":
                    var ContentType = login["ContentType"] as string;
                    if (String.IsNullOrEmpty(ContentType))
                    {
                        return false;
                    }
                    var value = login["Content"] as string;
                    if (String.IsNullOrEmpty(value))
                    {
                        return false;
                    }
                    break;
            }
            var Finish = login["Finish"] as string;
            if (String.IsNullOrEmpty(Finish))
            {
                return false;
            }
            return true;
        }
        void InitHeader(String sConf)
        {


            if (String.IsNullOrEmpty(sConf) == false)
            {

                foreach (var k in sConf.Split('\n', ','))
                {

                    var v = k.Trim();
                    if (String.IsNullOrEmpty(v) == false && String.Equals(k, "none") == false)
                    {

                        var nindex = v.IndexOf(':');
                        if (nindex == -1)
                        {
                            nindex = v.IndexOf(' ');
                            if (nindex == -1)
                            {
                                nindex = v.IndexOf('\t');
                            }
                        }
                        //var key = v;
                        if (nindex > -1)
                        {
                            var mv = v.Substring(nindex + 1).Trim();//.ToLower();
                            var key = v.Substring(0, nindex).Trim();
                            _HeaderConf[key] = mv;

                        }
                    }

                }




            }
        }

        void InitStatic(String sConf)
        {


            if (String.IsNullOrEmpty(sConf) == false)
            {
                if (sConf.Trim().StartsWith("{"))
                {
                    var v = UMC.Data.JSON.Deserialize(sConf) as Hashtable;
                    if (v != null)
                    {
                        var aem = v.GetEnumerator();
                        while (aem.MoveNext())
                        {
                            _StatusPage[aem.Key as String] = -1;
                        }
                    }
                }
                else
                {
                    foreach (var k in sConf.Split('\n', ','))
                    {

                        var v = k.Trim();
                        if (String.IsNullOrEmpty(v) == false && String.Equals(k, "none") == false)
                        {

                            var nindex = v.IndexOf(':');
                            if (nindex == -1)
                            {
                                nindex = v.IndexOf(' ');
                                if (nindex == -1)
                                {
                                    nindex = v.IndexOf('\t');
                                }
                            }
                            var key = v;
                            if (nindex > -1)
                            {
                                var mv = v.Substring(nindex + 1).Trim().ToLower();
                                key = v.Substring(0, nindex).Trim();
                                switch (mv)
                                {
                                    case "a":
                                    case "all":
                                        _StatusPage[key] = 0;
                                        break;
                                    case "u":
                                    case "user":
                                        _StatusPage[key] = 2;
                                        break;
                                    case "one":
                                        _StatusPage[key] = 1;
                                        break;
                                    default:
                                        _StatusPage[key] = UMC.Data.Utility.IntParse(mv, -1);
                                        break;
                                }
                            }
                            else
                            {
                                _StatusPage[key] = -1;
                            }
                        }

                    }


                }

            }
        }

        void InitHost(String sConf)
        {

            var union = Data.WebResource.Instance().Provider["union"] ?? ".";

            if (String.IsNullOrEmpty(sConf) == false)
            {
                if (sConf.Trim().StartsWith("{"))
                {
                    var v = UMC.Data.JSON.Deserialize(Site.StaticConf) as Hashtable;
                    if (v != null)
                    {
                        var aem = v.GetEnumerator();
                        while (aem.MoveNext())
                        {
                            if (_HostPage.ContainsKey(aem.Key as String) == false)
                            {
                                _HostPage[aem.Key as String] = new ReplaceSetting() { Model = HostReplaceModel.Replace, Hosts = new Dictionary<string, Uri>() };
                            }
                            ReplaceSetting replaceSetting = _HostPage[aem.Key as String];
                            HostReplaceModel hostReplace = replaceSetting.Model;
                            var mv = aem.Value.ToString().Split(',', ' ', '\t');

                            foreach (var kv in mv)
                            {
                                if (String.IsNullOrEmpty(kv) == false)
                                {
                                    var vk = kv.Trim();
                                    switch (vk)
                                    {
                                        default:
                                            break;
                                        case "rp":
                                            hostReplace |= HostReplaceModel.Replace;
                                            break;
                                        case "rm":
                                            hostReplace |= HostReplaceModel.Remove;
                                            break;
                                        case "input":
                                        case "in":
                                            hostReplace |= HostReplaceModel.Input;
                                            break;
                                        case "cdn":
                                            hostReplace |= HostReplaceModel.CDN;
                                            break;
                                        case "CDN":
                                            hostReplace |= HostReplaceModel.Script;
                                            break;


                                    }
                                }
                            }
                            replaceSetting.Model = hostReplace;


                        }
                    }
                }
                else
                {
                    foreach (var k in sConf.Split('\n'))
                    {

                        var v = k.Trim();
                        if (String.IsNullOrEmpty(v) == false && String.Equals(k, "none") == false)
                        {

                            var nindex = v.IndexOf(':');
                            if (nindex == -1)
                            {
                                nindex = v.IndexOf(' ');
                                if (nindex == -1)
                                {
                                    nindex = v.IndexOf('\t');
                                }
                            }
                            var key = v;
                            if (nindex > -1)
                            {
                                var mv = v.Substring(nindex + 1).Split(',', ' ', '\t');
                                key = v.Substring(0, nindex).Trim();
                                if (_HostPage.ContainsKey(key) == false)
                                {
                                    _HostPage[key] = new ReplaceSetting() { Model = HostReplaceModel.Replace, Hosts = new System.Collections.Generic.Dictionary<String, Uri>() };
                                }
                                ReplaceSetting replaceSetting = _HostPage[key];
                                HostReplaceModel hostReplace = replaceSetting.Model;
                                var list = replaceSetting.Hosts;

                                foreach (var kv in mv)
                                {
                                    if (String.IsNullOrEmpty(kv) == false)
                                    {

                                        var vk = kv.Trim();
                                        switch (vk)
                                        {
                                            default:
                                                if (String.IsNullOrEmpty(vk))
                                                {
                                                    hostReplace |= HostReplaceModel.Replace;
                                                }
                                                else
                                                {
                                                    var sit = DataFactory.Instance().Site(vk);
                                                    if (sit != null)
                                                    {
                                                        var doms = sit.Domain.Split(',', '\n');
                                                        foreach (var dName in doms)
                                                        {
                                                            var dName2 = dName.Trim();
                                                            var url = String.Empty;
                                                            if (String.IsNullOrEmpty(dName2) == false)
                                                            {
                                                                if (dName2.EndsWith("]"))
                                                                {
                                                                    var vin = dName2.LastIndexOf("[");
                                                                    if (vin > -1)
                                                                    {
                                                                        url = dName2.Substring(0, vin).TrimEnd(']', ' ').Trim();
                                                                    }
                                                                    else
                                                                    {
                                                                        url = dName2.Substring(0, vin).TrimEnd(']', ' ').Trim();
                                                                    }
                                                                    var sIndex = url.LastIndexOf('/');
                                                                    if (sIndex > 0)
                                                                    {

                                                                        url = url.Substring(0, sIndex);
                                                                    }
                                                                }
                                                                else if (v.IndexOf('@') == -1)
                                                                {
                                                                    url = dName2;
                                                                    var sIndex = url.LastIndexOf('/');
                                                                    if (sIndex > 0)
                                                                    {
                                                                        url = url.Substring(0, sIndex);
                                                                    }

                                                                }
                                                            }
                                                            if (String.IsNullOrEmpty(url) == false)
                                                            {
                                                                var surl = new Uri(url);
                                                                if (String.IsNullOrEmpty(sit.Host) == false)
                                                                {
                                                                    surl = new Uri(url.Replace(surl.Host, sit.Host));
                                                                }


                                                                list[String.Format("{0}{1}{2}", sit.Root, union, DataFactory.Instance().WebDomain())] = surl;// new Uri(String.f);

                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                                break;
                                            case "rp":
                                                hostReplace |= HostReplaceModel.Replace;
                                                break;
                                            case "rm":
                                                hostReplace |= HostReplaceModel.Remove;
                                                break;
                                            case "input":
                                            case "in":
                                                hostReplace |= HostReplaceModel.Input;
                                                break;
                                        }
                                    }
                                }
                                replaceSetting.Model = hostReplace;

                            }
                        }

                    }


                }

            }
        }

        public static Guid MD5Key(params object[] keys)
        {
            var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(String.Join(",", keys))));


        }
        public static String[] Config(String sConf)
        {
            var saticPagePath = new List<String>();

            if (String.IsNullOrEmpty(sConf) == false)
            {
                if (sConf.Trim().StartsWith("{"))
                {
                    var auth = new Hashtable();

                    var v = UMC.Data.JSON.Deserialize(sConf) as Hashtable;
                    if (v != null)
                    {
                        auth = v;
                    }


                    var aem = auth.GetEnumerator();
                    while (aem.MoveNext())
                    {
                        saticPagePath.Add((aem.Key as string).Trim());
                    }

                }
                else
                {
                    foreach (var k in sConf.Split(',', ' ', '\t', '\n'))
                    {

                        var v = k.Trim();
                        if (String.IsNullOrEmpty(v) == false && String.Equals("none", v) == false)
                        {
                            saticPagePath.Add(v);
                        }

                    }

                }
            }
            return saticPagePath.ToArray();
        }
        public String Home
        {
            get;
            private set;
        }
        public String Root
        {
            get; set;
        }

        public Entities.Site Site
        {
            get; private set;
        }

        public String[] Domains
        {
            get;
            private set;
        }
        public String[] LogPathConf
        {
            get;
            private set;
        }
        //public String[] StaticPagePath
        //{
        //    get;
        //    private set;
        //}

        public String[] AllowPath
        {
            get;
            private set;
        }
        public String[] LogoutPath
        {
            get;
            private set;
        }
        public String[] OutputCookies
        {
            get;
            private set;
        }
        public String[] AdminConf
        {
            get;
            private set;
        }
        public String[] AppendJSConf
        {

            get;
            private set;
        }


        public System.Collections.Generic.Dictionary<String, String> HeaderConf
        {
            get
            {
                return _HeaderConf;
            }
        }
        System.Collections.Generic.Dictionary<String, String> _HeaderConf = new Dictionary<string, String>();

        public System.Collections.Generic.Dictionary<String, int> StatusPage
        {
            get
            {
                return _StatusPage;
            }
        }
        System.Collections.Generic.Dictionary<String, int> _StatusPage = new Dictionary<string, int>();


        System.Collections.Generic.Dictionary<String, String[]> _test = new System.Collections.Generic.Dictionary<string, String[]>();

        public System.Collections.Generic.Dictionary<String, String[]> Test
        {

            get
            {
                return _test;
            }
        }
        System.Collections.Generic.Dictionary<String, ReplaceSetting> _HostPage = new System.Collections.Generic.Dictionary<string, ReplaceSetting>();

        public System.Collections.Generic.Dictionary<String, ReplaceSetting> HostPage
        {

            get
            {
                return _HostPage;
            }
        }

        System.Collections.Generic.Dictionary<String, String> _subSite = new System.Collections.Generic.Dictionary<string, string>();
        public System.Collections.Generic.Dictionary<String, String> SubSite
        {
            get
            {
                return _subSite;

            }
        }
    }
}
