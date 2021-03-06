﻿// SPDX-License-Identifier: Apache-2.0
// Licensed to the Ed-Fi Alliance under one or more agreements.
// The Ed-Fi Alliance licenses this file to you under the Apache License, Version 2.0.
// See the LICENSE and NOTICES files in the project root for more information.
 
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Http;
using EdFi.Ods.Api.Models.Requests.Students.EdFi;
using EdFi.Ods.Api.Services.Controllers.Students.EdFi;
using EdFi.Ods.Api.Services.Extensions;
using EdFi.Ods.Common;
using EdFi.Ods.Common.Extensions;
using EdFi.Ods.Common.InversionOfControl;
using EdFi.Ods.Pipelines.Factories;
using Newtonsoft.Json;
using NUnit.Framework;
using Shouldly;
using Test.Common;

namespace EdFi.Ods.Tests.EdFi.Ods.WebApi.Controllers
{
    public class StudentPostTests
    {
        private const string ExpectedUri = "http://localhost/api/ods/v3/ed-fi/students";

        [TestFixture]
        public class When_posting_a_non_existing_student : TestBase
        {
            private Guid _id;
            private HttpResponseMessage _responseMessage;
            private IServiceLocator _locator;

            [OneTimeSetUp]
            public void Setup()
            {
                _locator = TestControllerBuilder.GetWindsorContainer();

                var pipelineFactory =
                    new PipelineFactory(
                        _locator, null, null, null, new SingleStepPipelineProviderForTest(typeof(PersistNewModel<,,,>)), null);

                var controller = TestControllerBuilder.GetController<StudentsController>(pipelineFactory, _id.ToString("N"));
                _id = Guid.NewGuid();

                _responseMessage = controller
                    .Post( new StudentPost {ETag = _id.ToString("n")})
                    .GetResultSafely()
                    .ExecuteAsync(new CancellationToken())
                    .GetResultSafely();
            }

            [Test]
            public void Should_contain_location_header_in_response()
            {
                _responseMessage.Headers.Location.AbsoluteUri
                    .ShouldBe(string.Format("{0}/{1}", ExpectedUri, _id.ToString("n")));
            }

            [Test]
            public void Should_contain_updated_etag_in_response()
            {
                var etagprovider = _locator.Resolve<IETagProvider>();

                _responseMessage.Headers.ETag.ShouldBe(
                    new EntityTagHeaderValue(
                        etagprovider.GetETag(_id.ToString("n"))
                            .Quoted()));
            }

            [Test]
            public void Should_return_created_result()
            {
                _responseMessage.StatusCode.ShouldBe(HttpStatusCode.Created);
            }
        }

        [TestFixture]
        public class When_posting_an_existing_student : TestBase
        {
            private Guid _id;
            private HttpResponseMessage _responseMessage;
            private IServiceLocator _container;

            [OneTimeSetUp]
            public void Setup()
            {
                _container = TestControllerBuilder.GetWindsorContainer();

                var pipelineFactory =
                    new PipelineFactory(
                        _container, null, null, null, new SingleStepPipelineProviderForTest(typeof(PersistExistingModel<,,,>)),
                        null);

                var controller = TestControllerBuilder.GetController<StudentsController>(pipelineFactory, _id.ToString("N"));
                _id = Guid.NewGuid();

                _responseMessage = controller.Post(
                        new StudentPost {ETag = _id.ToString("n")})
                    .GetResultSafely()
                    .ExecuteAsync(new CancellationToken())
                    .GetResultSafely();
            }

            [Test]
            public void Should_contain_location_header_in_response()
            {
                _responseMessage.Headers.Location.AbsoluteUri
                    .ShouldBe(string.Format("{0}/{1}", ExpectedUri, _id.ToString("n")));
            }

            [Test]
            public void Should_contain_updated_etag_in_response()
            {
                var etagprovider = _container.Resolve<IETagProvider>();

                _responseMessage.Headers.ETag.ShouldBe(
                    new EntityTagHeaderValue(
                        etagprovider.GetETag(_id.ToString("n"))
                            .Quoted()));
            }

            [Test]
            public void Should_return_ok()
            {
                _responseMessage.StatusCode.ShouldBe(HttpStatusCode.OK);
            }
        }

        [TestFixture]
        public class When_posting_a_student_with_id_set : TestBase
        {
            private Guid _id;
            private HttpResponseMessage _responseMessage;

            [OneTimeSetUp]
            public void Setup()
            {
                var container = TestControllerBuilder.GetWindsorContainer();

                var pipelineFactory =
                    new PipelineFactory(
                        container, null, null, null, new SingleStepPipelineProviderForTest(typeof(PersistNewModel<,,,>)), null);

                var controller = TestControllerBuilder.GetController<StudentsController>(pipelineFactory, _id.ToString("N"));
                _id = Guid.NewGuid();

                _responseMessage = controller.Post(
                        new StudentPost
                        {
                            ETag = "etag",
                            Id = _id
                        })
                    .GetResultSafely()
                    .ExecuteAsync(new CancellationToken())
                    .GetResultSafely();
            }

            [Test]
            public void Should_return_bad_request()
            {
                _responseMessage.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            }

            [Test]
            public void Should_return_message()
            {
                var result = _responseMessage.Content.ReadAsStringAsync()
                    .Result;

                var resource = JsonConvert.DeserializeObject<HttpError>(result);

                resource["Message"]
                    .ShouldBe("Resource identifiers cannot be assigned by the client.");
            }
        }

        [TestFixture]
        public class When_posting_a_student_causes_unique_id_exception : TestBase
        {
            private Guid _id;
            private HttpResponseMessage _responseMessage;

            [OneTimeSetUp]
            public void Setup()
            {
                var container = TestControllerBuilder.GetWindsorContainer();

                var pipelineFactory =
                    new PipelineFactory(
                        container, null, null, null,
                        new SingleStepPipelineProviderForTest(typeof(InsertUniqueIdExceptionStep<,,,>)), null);

                var controller = TestControllerBuilder.GetController<StudentsController>(pipelineFactory, _id.ToString("N"));
                _id = Guid.NewGuid();

                _responseMessage = controller.Post(new StudentPost())
                    .GetResultSafely()
                    .ExecuteAsync(new CancellationToken())
                    .GetResultSafely();
            }

            [Test]
            public void Should_return_conflict()
            {
                _responseMessage.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            }

            [Test]
            public void Should_return_message()
            {
                var result = _responseMessage.Content.ReadAsStringAsync()
                    .Result;

                var resource = JsonConvert.DeserializeObject<HttpError>(result);

                var expression = new Regex(
                    @"^The value unknown supplied for property '(?<ForeignKeyName>\w+)' of entity '(?<TableName>\w+)' is not unique.");

                expression.Match(resource.Message)
                    .Success.ShouldBeTrue();
            }
        }

        [TestFixture]
        public class When_posting_a_student_causes_referencial_exception : TestBase
        {
            private Guid _id;
            private HttpResponseMessage _responseMessage;

            [OneTimeSetUp]
            public void Setup()
            {
                var container = TestControllerBuilder.GetWindsorContainer();

                var pipelineFactory =
                    new PipelineFactory(
                        container, null, null, null,
                        new SingleStepPipelineProviderForTest(typeof(InsertReferentialExceptionStep<,,,>)), null);

                var controller = TestControllerBuilder.GetController<StudentsController>(pipelineFactory, _id.ToString("N"));
                _id = Guid.NewGuid();

                _responseMessage = controller.Post(new StudentPost())
                    .GetResultSafely()
                    .ExecuteAsync(new CancellationToken())
                    .GetResultSafely();
            }

            [Test]
            public void Should_return_conflict()
            {
                _responseMessage.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            }

            [Test]
            public void Should_return_message()
            {
                var result = _responseMessage.Content.ReadAsStringAsync()
                    .Result;

                var resource = JsonConvert.DeserializeObject<HttpError>(result);

                var expression = new Regex(
                    @"^The value supplied for the related '(?<ConstraintName>\w+)' resource does not exist.");

                expression.Match(resource.Message)
                    .Success.ShouldBeTrue();
            }
        }

        [TestFixture]
        public class When_posting_a_student_causes_validation_exception : TestBase
        {
            private Guid _id;
            private HttpResponseMessage _responseMessage;

            [OneTimeSetUp]
            public void Setup()
            {
                var container = TestControllerBuilder.GetWindsorContainer();

                var pipelineFactory =
                    new PipelineFactory(
                        container, null, null, null, new SingleStepPipelineProviderForTest(typeof(ValidationExceptionStep<,,,>)),
                        null);

                var controller = TestControllerBuilder.GetController<StudentsController>(pipelineFactory, _id.ToString("N"));
                _id = Guid.NewGuid();

                _responseMessage = controller.Post(new StudentPost())
                    .GetResultSafely()
                    .ExecuteAsync(new CancellationToken())
                    .GetResultSafely();
            }

            [Test]
            public void Should_return_bad_request()
            {
                _responseMessage.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            }

            [Test]
            public void Should_return_message()
            {
                var result = _responseMessage.Content.ReadAsStringAsync()
                    .GetResultSafely();

                var resource = JsonConvert.DeserializeObject<HttpError>(result);

                resource["Message"]
                    .ShouldBe("Exception for testing");
            }
        }

        [TestFixture]
        public class When_posting_student_unauthorized : TestBase
        {
            [Test]
            public void Should_return_forbidden()
            {
                var container = TestControllerBuilder.GetWindsorContainer();
                var id = Guid.NewGuid();

                var pipelineFactory =
                    new PipelineFactory(
                        container, null, null, null,
                        new SingleStepPipelineProviderForTest(typeof(EdFiSecurityExceptionStep<,,,>)), null);

                var controller = TestControllerBuilder.GetController<StudentsController>(pipelineFactory, id.ToString("N"));

                var responseMessage = controller.Post(new StudentPost())
                    .GetResultSafely()
                    .ExecuteAsync(new CancellationToken())
                    .GetResultSafely();

                responseMessage.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            }
        }

        [TestFixture]
        public class When_posting_student_throws_unhandled_exception : TestBase
        {
            //private Guid _id;
            private HttpResponseMessage _responseMessage;

            [OneTimeSetUp]
            public void Setup()
            {
                var id = Guid.NewGuid();
                var container = TestControllerBuilder.GetWindsorContainer();

                var pipelineFactory =
                    new PipelineFactory(
                        container, null, null, null, new SingleStepPipelineProviderForTest(typeof(UnhandledExceptionStep<,,,>)),
                        null);

                var controller = TestControllerBuilder.GetController<StudentsController>(pipelineFactory, id.ToString("N"));

                _responseMessage = controller.Post(new StudentPost())
                    .GetResultSafely()
                    .ExecuteAsync(new CancellationToken())
                    .GetResultSafely();
            }

            [Test]
            public void Should_return_internal_server_error()
            {
                _responseMessage.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            }

            [Test]
            public void Should_return_message()
            {
                var result = _responseMessage.Content.ReadAsStringAsync()
                    .GetResultSafely();

                var resource = JsonConvert.DeserializeObject<HttpError>(result);
                resource.Message.ShouldBe("An unexpected error occurred on the server.");
            }
        }
    }
}
