using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FutureOfLatinos.Models.Responses
{
    /// <summary>
    /// This is an example of a Generic class that you will gain an understanding of
    /// as you progress through the training.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class LoginResponse<T> : ItemResponse<T>
    {
        public List<int> PersonID { get; set; }
    }
}
