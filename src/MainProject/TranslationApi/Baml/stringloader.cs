//-------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Description: String Loader class, loading strings from StringTable Resource. 
//-------------------------------------------------------------------------------


using System;
using System.Resources;
using System.Globalization;

namespace TranslationApi.Baml
{
    internal class StringLoader
    {
        public static string Get(string id, params object[] args)
        {
            //string message = _resourceManager.GetString(id);
            //if (message != null)
            //{
            //    // Apply arguments to formatted string (if applicable)
            //    if (args != null && args.Length > 0)
            //    {
            //        message = String.Format(CultureInfo.CurrentCulture, message, args);
            //    }
            //}
            //return message;

            return "";
        }
        // Get exception string resources for current locale
        private static ResourceManager _resourceManager = new ResourceManager("Resources.StringTable", typeof(StringLoader).Assembly);
    }//endof class StringLoader    
}//endof namespace
