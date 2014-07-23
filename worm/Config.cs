using System;

namespace worm
{
    public class Config
    {
        public string Network { get; set; }
        public string Nick { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string RealName { get; set; }
        public string[] Channels { get; set; }
        public string[] Admins { get; set; }
        public string RedditUser { get; set; }
        public string RedditPassword { get; set; }
    }
}
