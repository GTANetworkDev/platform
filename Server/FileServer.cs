using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using GTANetworkShared;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Hosting;
using Nancy;
using Newtonsoft.Json;
using Owin;

namespace GTANetworkServer
{
    public class FileServer : IDisposable
    {
        private IDisposable _server;

        public void Start(int port)
        {
            var url = "http://+:" + port;

            try
            {
                _server = WebApp.Start<Startup>(url);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException is HttpListenerException)
                {
                    Program.Output("File server error: " + ex.InnerException.Message);
                }
                else
                {
                    Program.Output("File server error: ");
                    Program.Output(ex.ToString());
                }

                Program.Output("Reverting to UDP file server.");
                Program.ServerInstance.UseHTTPFileServer = false;
            }
        }

        public void Dispose()
        {
            _server?.Dispose();
        }
    }

    public class FileModule : NancyModule
    {
        public static Dictionary<string, List<FileDeclaration>> ExportedFiles = new Dictionary<string, List<FileDeclaration>>();

        public FileModule()
        {
            Get["/{resource}/{path*}"] = parameters =>
            {
                if (!ExportedFiles.ContainsKey((string) parameters.resource) ||
                    !ExportedFiles[(string) parameters.resource].Contains(parameters.path))
                    return 404;

                string fullFile = Path.Combine("resources" + Path.DirectorySeparatorChar + parameters.resource, parameters.path);

                if (File.Exists(fullFile))
                {
                    return
                        Response.AsFile(fullFile);
                }

                return 404;
            };

            Get["/manifest.json"] = _ =>
            {
                var resp = (Response) JsonConvert.SerializeObject(new FileManifest()
                {
                    exportedFiles = new Dictionary<string, List<FileDeclaration>>(ExportedFiles)
                });

                resp.ContentType = "application/json";

                return resp;
            };
        }
    }

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseNancy();
            app.UseStageMarker(PipelineStage.MapHandler);
        }
    }
}
