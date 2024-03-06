using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoSuchStudio.Common {
    public static class ExceptionExts {
        public static Exception RootCause(this Exception e) {
            return e.InnerException != null ? e.InnerException.RootCause() : e;
        }
    }
}
