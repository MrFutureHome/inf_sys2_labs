﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab2__stop_and_wait_
{
    public class Packet
    {
        public int SequenceNumber { get; set; }
        public string Data { get; set; } = string.Empty;
    }
}
