using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Swashbuckle.SwaggerGen.Generator;

namespace Swashbuckle.SwaggerGen.Application
{
    public class SwaggerGenMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ISwaggerProvider _swaggerProvider;
        private readonly TemplateMatcher _requestMatcher;
        private readonly JsonSerializer _swaggerSerializer;

        public SwaggerGenMiddleware(
            RequestDelegate next,
            ISwaggerProvider swaggerProvider,
            string routeTemplate)
        {
            _next = next;
            _swaggerProvider = swaggerProvider;
            _requestMatcher = new TemplateMatcher(TemplateParser.Parse(routeTemplate), new RouteValueDictionary());
            _swaggerSerializer = new JsonSerializer
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new SwaggerGenContractResolver()
            };
        }

        public async Task Invoke(HttpContext httpContext)
        {
            string apiVersion;
            if (!RequestingSwaggerDocs(httpContext.Request, out apiVersion))
            {
                await _next(httpContext);
                return;
            }

            var basePath = GetPathBase(httpContext);

            var swagger = _swaggerProvider.GetSwagger(apiVersion, null, basePath);

            RespondWithSwaggerJson(httpContext.Response, swagger);
        }

        private bool RequestingSwaggerDocs(HttpRequest request, out string apiVersion)
        {
            apiVersion = null;
            if (request.Method != "GET") return false;

            RouteValueDictionary routeValues = new RouteValueDictionary();
            bool result = _requestMatcher.TryMatch(request.Path, routeValues);
            if (!result || !routeValues.ContainsKey("apiVersion")) return false;

            apiVersion = routeValues["apiVersion"].ToString();
            return true;
        }

        private void RespondWithSwaggerJson(HttpResponse response, SwaggerDocument swagger)
        {
            response.StatusCode = 200;
            response.ContentType = "application/json";

            using (var writer = new StreamWriter(response.Body))
            {
                _swaggerSerializer.Serialize(writer, swagger);
            }
        }

        private string GetPathBase(HttpContext httpContext)
        {
            List<string> pathParts = new List<string>();
            
            StringValues forwardedPath;
            if (httpContext.Request.Headers.TryGetValue("X-Forwarded-PathBase", out forwardedPath) && !string.IsNullOrWhiteSpace(forwardedPath))
            {
                pathParts.Add(forwardedPath.ToString().Trim('/'));
            }
            
            var basePath = httpContext.Request.PathBase.ToString();
            if (!string.IsNullOrWhiteSpace(basePath))
            {
                pathParts.Add(basePath.Trim('/'));
            }

            var swaggerUrl = "/" + string.Join("/", pathParts);
            
            return swaggerUrl;
        }
    }
}
