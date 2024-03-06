using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NoSuchStudio.Common
{
    public class IllegalStateException : Exception
    {
        public IllegalStateException(string msg) : base(msg)
        {

        }
    }
}
