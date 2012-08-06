using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Web.Http;
using System.Net.Http;
using System.Net.Http.Headers;

using System.IO;
using System.Web.Script.Serialization;

namespace POIProxy.Controllers
{
    public class SessionsController : ApiController
    {

        public HttpResponseMessage GetByContentId(int contentId)
        {
            POISessionManager manager = POIProxyGlobalVar.Kernel.mySessionManager;

            JavaScriptSerializer js = new JavaScriptSerializer();
            string jsonString = js.Serialize(manager.GetSessionsByContent(contentId));

            var response = new HttpResponseMessage();
            response.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            return response;
        }

        public HttpResponseMessage GetSlideBySessionId(int sessionId, int slideIndex)
        {
            var response = new HttpResponseMessage();

            //Find the session
            var registery = POIProxyGlobalVar.Kernel.mySessionManager.Registery;
            var session = registery.GetSessionById(sessionId);

            if (session != null)
            {
                var slide = session.PresController.CurPres.SlideAtIndex(slideIndex);

                if (slide != null)
                {
                    StreamReader reader = new StreamReader(slide.Source.LocalPath);

                    var content = new StreamContent(reader.BaseStream);
                    content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                    response.Content = content;
                }
                else
                {
                    response.StatusCode = System.Net.HttpStatusCode.NoContent;
                }
                
            }
            else
            {
                response.StatusCode = System.Net.HttpStatusCode.NoContent;
            }

            return response;
        }
    }
}
