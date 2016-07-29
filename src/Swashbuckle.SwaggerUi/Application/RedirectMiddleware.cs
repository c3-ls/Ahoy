﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace Swashbuckle.Application
{
    public class RedirectMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _toPath;
        private readonly TemplateMatcher _requestMatcher;

        public RedirectMiddleware(
            RequestDelegate next,
            string fromPath,
            string toPath)
        {
            _next = next;
            _toPath = toPath;
            _requestMatcher = new TemplateMatcher(TemplateParser.Parse(fromPath), new RouteValueDictionary());
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (!RequestingFromPath(httpContext.Request))
            {
                await _next(httpContext);
                return;
            }

            RespondWithRedirect(httpContext.Response, httpContext.Request.PathBase);
        }

        private bool RequestingFromPath(HttpRequest request)
        {
            if (request.Method != "GET") return false;

            var routeValues = new RouteValueDictionary();
            return _requestMatcher.TryMatch(request.Path, routeValues);
        }

        private void RespondWithRedirect(HttpResponse response, string pathBase)
        {
            response.Redirect(pathBase + "/" + _toPath);
        }
    }
}
