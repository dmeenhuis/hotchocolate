﻿#if ASPNETCLASSIC
namespace HotChocolate.AspNetClassic
#else
namespace HotChocolate.AspNetCore
#endif
{
    public static class HttpMethods
    {
        public const string Post = "POST";
        public const string Get = "GET";
    }
}
