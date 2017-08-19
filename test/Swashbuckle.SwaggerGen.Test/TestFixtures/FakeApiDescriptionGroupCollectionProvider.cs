﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Moq;
using Microsoft.AspNetCore.Mvc.DataAnnotations.Internal;
using Microsoft.AspNetCore.Mvc.Internal;
using Newtonsoft.Json;

namespace Swashbuckle.SwaggerGen.TestFixtures
{
    public class FakeApiDescriptionGroupCollectionProvider : IApiDescriptionGroupCollectionProvider
    {
        private readonly List<ControllerActionDescriptor> _actionDescriptors;

        public FakeApiDescriptionGroupCollectionProvider()
        {
            _actionDescriptors = new List<ControllerActionDescriptor>();
        }

        public FakeApiDescriptionGroupCollectionProvider Add(
            string httpMethod,
            string routeTemplate,
            string actionFixtureName,
            string controllerFixtureName = "NotAnnotated"
        )
        {
            _actionDescriptors.Add(
                CreateActionDescriptor(httpMethod, routeTemplate, actionFixtureName, controllerFixtureName));
            return this;
        }

        public ApiDescriptionGroupCollection ApiDescriptionGroups
        {
            get
            {
                var apiDescriptions = GetApiDescriptions();
                var group = new ApiDescriptionGroup("default", apiDescriptions);
                return new ApiDescriptionGroupCollection(new[] { group }, 1);
            }
        }

        private ControllerActionDescriptor CreateActionDescriptor(
            string httpMethod,
            string routeTemplate,
            string actionFixtureName,
            string controllerFixtureName
        )
        {
            var descriptor = new ControllerActionDescriptor();
            descriptor.SetProperty(new ApiDescriptionActionData());

            descriptor.ActionConstraints = new List<IActionConstraintMetadata>();
            if (httpMethod != null)
                descriptor.ActionConstraints.Add(new HttpMethodActionConstraint(new[] { httpMethod }));

            descriptor.AttributeRouteInfo = new AttributeRouteInfo { Template = routeTemplate };

            descriptor.MethodInfo = typeof(FakeActions).GetMethod(actionFixtureName);
            if (descriptor.MethodInfo == null)
                throw new InvalidOperationException(
                    string.Format("{0} is not declared in ActionFixtures", actionFixtureName));

            descriptor.Parameters = descriptor.MethodInfo.GetParameters()
                .Select(paramInfo => new ParameterDescriptor
                    {
                        Name = paramInfo.Name,
                        ParameterType = paramInfo.ParameterType,
                        BindingInfo = BindingInfo.GetBindingInfo(paramInfo.GetCustomAttributes(false))
                    })
                .ToList();

            var controllerType = typeof(FakeControllers).GetNestedType(controllerFixtureName);
            if (controllerType == null)
                throw new InvalidOperationException(
                    string.Format("{0} is not declared in ControllerFixtures", controllerFixtureName));
            descriptor.ControllerTypeInfo = controllerType.GetTypeInfo();

            return descriptor;
        }

        private IReadOnlyList<ApiDescription> GetApiDescriptions()
        {
            var context = new ApiDescriptionProviderContext(_actionDescriptors);

            var options = new MvcOptions();
            options.OutputFormatters.Add(new JsonOutputFormatter(new JsonSerializerSettings(), null));

            var optionsAccessor = new Mock<IOptions<MvcOptions>>();
            optionsAccessor.Setup(o => o.Value).Returns(options);

            var constraintResolver = new Mock<IInlineConstraintResolver>();
            constraintResolver.Setup(i => i.ResolveConstraint("int")).Returns(new IntRouteConstraint());

            var provider = new DefaultApiDescriptionProvider(
                optionsAccessor.Object,
                constraintResolver.Object,
                CreateModelMetadataProvider()
            );

            provider.OnProvidersExecuting(context);
            provider.OnProvidersExecuted(context);
            return new ReadOnlyCollection<ApiDescription>(context.Results);
        }

        private IModelMetadataProvider CreateModelMetadataProvider()
        {
            var metadataDetailsProvider = new DefaultCompositeMetadataDetailsProvider(
                new IMetadataDetailsProvider[]
                {
                    new DefaultBindingMetadataProvider(new ModelBindingMessageProvider
                    {
                        MissingBindRequiredValueAccessor = name => $"A value for the '{ name }' property was not provided.",
                        MissingKeyOrValueAccessor = () => $"A value is required.",
                        ValueMustNotBeNullAccessor = value => $"The value '{ value }' is invalid.",
                    }),
                    new DataAnnotationsMetadataProvider()
                }
            );
            return new DefaultModelMetadataProvider(metadataDetailsProvider);
        }
    }
}