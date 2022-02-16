using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UMC.Data;
using UMC.Data.Entities;
using UMC.Data.Sql;
using UMC.Net;

namespace UMC.Proxy.Entities
{
    [Web.Mapping]
    public class Initializer : UMC.Data.Sql.Initializer
    {
        public Initializer()
        {
            this.Setup(new Site { Root = String.Empty }, new Site
            {
                OutputCookies = String.Empty,
                HostReConf = String.Empty,
                LogoutPath = String.Empty,
                LogPathConf = String.Empty,
                StaticConf = String.Empty,
                Conf = String.Empty,
                AdminConf = String.Empty,
                AppendJSConf = String.Empty,
                AuthConf = String.Empty
            });
            this.Setup(new HostSite { Host = String.Empty });
            this.Setup(new Cookie { user_id = Guid.Empty, Domain = String.Empty, IndexValue = 0 }, new Cookie { Cookies = String.Empty, Config = String.Empty });
        }

        public override string Name => "Proxy";

        public override string Caption => "应用网关";

        public override string ProviderName => "defaultDbProvider";

        public override void Menu(IDictionary hash)
        {
            Data.DataFactory.Instance().Put(new Menu()
            {
                Icon = "\uea04",
                Caption = "应用网关",
                IsDisable = false,
                ParentId = Guid.Empty,
                Seq = 10,
                Id = Utility.Guid("#proxy", true),
                Url = "#proxy"

            });

        }
    }
}
