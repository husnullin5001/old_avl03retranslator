﻿using System;
using TcpServer.Core.Properties;

namespace TcpServer.Core.Mintrans
{
    public class MintransSettings
    {
        public MintransSettings()
        {
            this.Url = Settings.Default.Mintrans_Url;
            this.UserName = Settings.Default.Mintrans_UserName;
            this.Password = Settings.Default.Mintrans_Password;
            this.ImeiFileName = Settings.Default.Mintrans_ImeiFileName;
            this.Enabled = Settings.Default.Mintrans_Enabled;
        }

        public string Url { get; private set; }
        public string UserName { get; private set; }
        public string Password { get; private set; }
        public string ImeiFileName { get; private set; }
        public bool Enabled { get; private set; }
    }
}