using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Filters;
using System.Net.Http.Headers;

using POILibCommunication;
using System.IO;

namespace POIProxy.Controllers
{
    public class AllowCrossSiteJsonAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {
            if (actionExecutedContext.Response != null)
                actionExecutedContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");

            base.OnActionExecuted(actionExecutedContext);
        }
    }

    //[AllowCrossSiteJson]
    public class AudioUploadController : ApiController
    {
        public string GetById(int id)
        {
            return id.ToString();
        }

        public string GetByCategory(int cat, int id)
        {
            
            return "Hello";
        
        }

        public HttpResponseMessage OptionsAudio()
        {
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);

            return response;
        }

        public HttpResponseMessage PostAudio()
        {
            

            //POIGlobalVar.POIDebugLog(Request);
            //POIGlobalVar.POIDebugLog(Request.Content);
            //POIGlobalVar.POIDebugLog(Request.Content.Headers);

            if (Request.Content.Headers.ContentType.MediaType == "audio/wav")
            {
                POIGlobalVar.POIDebugLog("Audio wav!");
                ProcessAudio(Request.Content as StreamContent);
            }
            else
            {
                POIGlobalVar.POIDebugLog("Not audio!");
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }

            //Return 200 OK to the client
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);

            return response;
        }

        private async void ProcessAudio(StreamContent content)
        {
            try
            {
                FileStream fs = new FileStream("test2.wav", FileMode.Create);
                POIGlobalVar.POIDebugLog(Directory.GetCurrentDirectory());
                await content.CopyToAsync(fs);
                fs.Close();
            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog(e);
            }
            
        }
    }
}
