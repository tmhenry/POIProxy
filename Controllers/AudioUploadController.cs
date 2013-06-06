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
            {
                actionExecutedContext.Response.Headers.Add("Access-Control-Allow-Origin", "http://localhost:8081");
                actionExecutedContext.Response.Headers.Add("Access-Control-Allow-Methods", "POST");
                actionExecutedContext.Response.Headers.Add("Access-Control-Allow-Headers", "origin, content-type");
                actionExecutedContext.Response.Headers.Add("Access-Control-Max-Age", "1728000");
                //actionExecutedContext.Response.Headers.Add("Content-Encoding", "gzip");
                actionExecutedContext.Response.Headers.Add("Connection", "Keep-Alive");
            }

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
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "origin, content-type");
            response.Headers.Add("Access-Control-Max-Age", "1728000");
            //response.Headers.Add("Content-Encoding", "gzip");
            //response.Headers.Add("Connection", "Keep-Alive");

            return response;
        }

        public HttpResponseMessage PostAudio(string userId, int sessionId)
        {
            //Get the session id and user information from the posted data
            POIGlobalVar.POIDebugLog(userId);
            POIGlobalVar.POIDebugLog(sessionId);

            //send the audio to the commander
            

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
            response.Content.Headers.Add("Content-type", "audio/wav");
            response.Content.Headers.Add("Content-length", Request.Content.Headers.ContentLength.ToString());

            return response;
        }

        private async void ProcessAudio(StreamContent content)
        {
            try
            {
                //Copy the audio bytes into memory
                MemoryStream ms = new MemoryStream();
                POIGlobalVar.POIDebugLog(Directory.GetCurrentDirectory());
                await content.CopyToAsync(ms);
                
                //Construct the audio comment
                POITextComment audioComment = new POITextComment(0, ms.GetBuffer());

            }
            catch (Exception e)
            {
                POIGlobalVar.POIDebugLog(e);
            }
            
        }
    }
}
