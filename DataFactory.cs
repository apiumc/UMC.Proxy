using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.IO.Compression;
using UMC.Data;
using UMC.Proxy.Entities;

namespace UMC.Proxy
{
    public class DataFactory
    {
        static DataFactory()
        {
            HotCache.NetDBRegister<UMC.Proxy.Entities.Site>("Root").Register("SiteKey").IsSyncData = true;
            HotCache.Register<UMC.Proxy.Entities.Cookie>("user_id", "Domain", "IndexValue");
            HotCache.Register<UMC.Proxy.Entities.HostSite>("Host").Register("Root", "Host");
            HotCache.ObjectRegister<UMC.Proxy.SiteConfig>("Root");


        }
        public static DataFactory Instance()
        {
            return _Instance;
        }
        static DataFactory _Instance = new DataFactory();
        public static void Instance(DataFactory dataFactory)
        {
            _Instance = dataFactory;
        }

        public virtual Site[] Site()
        {
            return HotCache.Cache<Site>().Get(new Proxy.Entities.Site(), "", new object[0]);

        }


        public virtual Site Site(String root)
        {
            return HotCache.Cache<Site>().Get(new Proxy.Entities.Site { Root = root });

        }
        public virtual Site Site(int siteKey)
        {
            return HotCache.Cache<Site>().Get(new Proxy.Entities.Site { SiteKey = siteKey });

        }

        public virtual void Put(HostSite host)
        {
            HotCache.Cache<HostSite>().Put(host);
        }
        public virtual HostSite HostSite(string host)
        {
            return HotCache.Cache<HostSite>().Get(new Entities.HostSite { Host = host });
        }
        public virtual HostSite[] Host(string root)
        {
            return HotCache.Cache<HostSite>().Get(new Entities.HostSite(), "Root", root);
        }
        public virtual void Delete(HostSite host)
        {
            HotCache.Cache<HostSite>().Delete(host);
        }
        public virtual Cookie Cookie(String domain, Guid user_id, int index)
        {
            return HotCache.Cache<Cookie>().Get(new Proxy.Entities.Cookie { Domain = domain, user_id = user_id, IndexValue = index });


        }
        public virtual Cookie[] Cookies(Guid user_id)
        {

            return HotCache.Cache<Cookie>().Get(new Proxy.Entities.Cookie
            {
                user_id = user_id
            }, "user_id", new object[0]);
        }
        public virtual Cookie[] Cookies(String domain, Guid user_id)
        {
            return Database.Instance().ObjectEntity<Cookie>()
                  .Where.And().Equal(new UMC.Proxy.Entities.Cookie
                  {
                      user_id = user_id,
                      Domain = domain
                  }).Entities.Query().OrderBy(r => r.IndexValue ?? 0).ToArray();
        }
        public virtual void Put(Site site)
        {
            var secret = Data.WebResource.Instance().Provider["appSecret"];
            if (String.IsNullOrEmpty(secret) == false)
            {
                site.Root = site.Root.ToLower();
                HotCache.Cache<Site>().Put(site);
            }
        }
        public virtual bool IsRegister()
        {
            var secret = Data.WebResource.Instance().Provider["appSecret"];
            if (String.IsNullOrEmpty(secret) == false)
            {
                return true;
            }
            return false;
        }
        public virtual void Delete(Site site)
        {
            HotCache.Cache<Site>().Delete(site);
        }
        public virtual void Delete(Cookie cookie)
        {
            if (String.IsNullOrEmpty(cookie.Domain) == false && cookie.user_id.HasValue)
            {
                if (cookie.IndexValue.HasValue == false)
                {
                    cookie.IndexValue = 0;
                }
                HotCache.Cache<Cookie>().Delete(cookie);
            }
        }
        public virtual void Put(Cookie cookie)
        {
            if (cookie.IndexValue.HasValue == false)
            {
                cookie.IndexValue = 0;
            }
            if (String.IsNullOrEmpty(cookie.Domain) == false && cookie.user_id.HasValue)
            {
                HotCache.Cache<Cookie>().Put(cookie);
            }

        }
        public virtual UMC.Data.Entities.Log[] Search(UMC.Data.Entities.Log search, int timeType)
        {
            int t;
            return HotCache.Cache<UMC.Data.Entities.Log>().Get(search, 0, timeType, out t);
        }
        public virtual String Evaluate(String js, params string[] args)
        {
            return "";
        }
        public virtual Stream Decompress(Stream response, string encoding)
        {
            switch (encoding)
            {
                case "gzip":
                    return new GZipStream(response, CompressionMode.Decompress);
                case "deflate":
                    return new DeflateStream(response, CompressionMode.Decompress);
                default:
                    return response;
            }
        }
        public virtual Stream Compress(Stream response, string encoding)
        {
            switch (encoding)
            {
                case "gzip":
                    return new GZipStream(response, CompressionMode.Compress);
                case "deflate":
                    return new DeflateStream(response, CompressionMode.Compress);
                default:
                    return response;
            }
        }

        public virtual string WebDomain()
        {
            return Data.WebResource.Instance().Provider["host"] ?? "/";
        }
        //public virtual string TempDirectory()
        //{
        //    return UMC.Data.Utility.MapPath("App_Data\\TEMP\\");
        //}

        public virtual SiteConfig SiteConfig(String root)
        {
            var siteConfig = HotCache.Cache<SiteConfig>().Get(new Proxy.SiteConfig { Root = root });
            if (siteConfig == null)
            {
                var site = this.Site(root);
                if (site != null)
                {
                    siteConfig = new SiteConfig(site); ;
                    HotCache.Cache<SiteConfig>().Put(siteConfig);

                }
            }
            return siteConfig;
        }

        //public virtual SiteWarn SiteWarn(String Domain, String path, int statusCode)
        //{
        //    return HotCache.Cache<SiteWarn>().Get(new Entities.SiteWarn { Domain = Domain, Path = path, StatusCode = statusCode });


        //}
        //public virtual void Warn(SiteWarn siteWarn)
        //{
        //    HotCache.Cache<SiteWarn>().Put(siteWarn);

        //}

        public virtual void Delete(Proxy.SiteConfig siteConfig)
        {

            HotCache.Cache<SiteConfig>().Delete(siteConfig);

        }
    }
}
