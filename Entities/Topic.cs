using System;
using System.Collections.Generic;
using System.Text;

namespace UMC.Proxy.Entities
{
    public enum TopicMethod
    {
        Get = 1, Delete = 2, Post = 4, Put = 8
    }
    public class Topic
    {
        public String Key
        {
            get;
            set;
        }
        public bool? IsSync
        {
            get; set;
        }
        public TopicMethod? Method
        {
            get; set;
        }
        public String Caption
        {
            get; set;
        }
    }
    public class Consumer
    {
        public long? Id { get; set; }
        public String Topic
        {
            get;
            set;
        }
        public String Url
        {
            get; set;
        }
        public String Caption
        {
            get; set;
        }
        public int? Status
        {
            get; set;
        }
        public int? Time
        {
            get; set;
        }
    }
    public class Producer
    {
        public long? Id { get; set; }
        public String Topic
        {
            get;
            set;
        }
        public TopicMethod Method
        {
            get; set;
        }
        public string ContentType { get; set; }
        public string Body
        {
            get; set;
        }
        public DateTime? SendTime;
    }
    public class Message
    {
        public String Topic
        {
            get;
            set;
        }
        public long? consumer_id
        {
            get; set;
        }
        public long? productor_id
        {
            get; set;
        }
        public int? Status
        {
            get; set;
        }
        public int? Duration
        {
            get; set;
        }
        public int? Time
        {
            get; set;
        }

        public DateTime? CreationTime
        {
            get; set;
        }

        public DateTime? ModifiedTime
        {
            get; set;
        }


        public string Result { get; set; }
    }


}
