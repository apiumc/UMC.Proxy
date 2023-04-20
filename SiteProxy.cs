using System;
using System.Collections.Generic;
using System.Text;

namespace UMC.Proxy
{

    public abstract class SiteProxy
    {
        public abstract bool Proxy(HttpProxy proxy); 
    }
}
