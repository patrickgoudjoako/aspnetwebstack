﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Results;
using Microsoft.TestCommon;
using Moq;

namespace System.Web.Http.ExceptionHandling
{
    public class LastChanceExceptionHandlerTests
    {
        [Fact]
        public void Constructor_IfInnerHandlerIsNull_Throws()
        {
            // Arrange
            IExceptionHandler innerHandler = null;

            // Act & Assert
            Assert.ThrowsArgumentNull(() => CreateProductUnderTest(innerHandler), "innerHandler");
        }

        [Fact]
        public void InnerHandler_ReturnsSpecifiedInstance()
        {
            // Arrange
            IExceptionHandler expectedInnerHandler = CreateDummyHandler();
            LastChanceExceptionHandler product = CreateProductUnderTest(expectedInnerHandler);

            // Act
            IExceptionHandler innerHandler = product.InnerHandler;

            // Assert
            Assert.Same(expectedInnerHandler, innerHandler);
        }

        [Fact]
        public void HandleAsync_DelegatesToInnerHandler()
        {
            Mock<IExceptionHandler> mock = new Mock<IExceptionHandler>(MockBehavior.Strict);
            Task expectedTask = CreateCompletedTask();
            mock
                .Setup(h => h.HandleAsync(It.IsAny<ExceptionHandlerContext>(), It.IsAny<CancellationToken>()))
                .Returns(expectedTask);
            IExceptionHandler innerHander = mock.Object;

            IExceptionHandler product = CreateProductUnderTest(innerHander);

            ExceptionHandlerContext expectedContext = CreateContext();

            using (CancellationTokenSource tokenSource = CreateCancellationTokenSource())
            {
                CancellationToken expectedCancellationToken = tokenSource.Token;

                // Act
                Task task = product.HandleAsync(expectedContext, expectedCancellationToken);

                // Assert
                Assert.Same(expectedTask, task);
                task.WaitUntilCompleted();
                mock.Verify(h => h.HandleAsync(expectedContext, expectedCancellationToken), Times.Once());
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void HandleAsync_IfIsTopLevelCatchBlockAndCanCreateExceptionResult_InitializesResult(bool includeDetail)
        {
            Mock<IExceptionHandler> mock = new Mock<IExceptionHandler>(MockBehavior.Strict);
            IHttpActionResult result = null;
            mock
                .Setup(h => h.HandleAsync(It.IsAny<ExceptionHandlerContext>(), It.IsAny<CancellationToken>()))
                .Returns<ExceptionHandlerContext, CancellationToken>((c, i) =>
                {
                    result = c != null ? c.Result : null;
                    return Task.FromResult(0);
                });
            IExceptionHandler innerHander = mock.Object;

            IExceptionHandler product = CreateProductUnderTest(innerHander);

            Exception expectedException = CreateDummyException();
            IContentNegotiator expectedContentNegotiator = CreateDummyContentNegotiator();

            using (HttpConfiguration configuration = CreateConfiguration())
            using (HttpRequestMessage expectedRequest = CreateRequest())
            {
                configuration.Services.Replace(typeof(IContentNegotiator), expectedContentNegotiator);
                configuration.Formatters.Clear();
                MediaTypeFormatter expectedFormatter = CreateDummyFormatter();
                configuration.Formatters.Add(expectedFormatter);

                ExceptionHandlerContext context = new ExceptionHandlerContext(new ExceptionContext
                {
                    Exception = expectedException,
                    RequestContext = new HttpRequestContext
                    {
                        Configuration = configuration,
                        IncludeErrorDetail = includeDetail
                    },
                    Request = expectedRequest,
                    IsTopLevelCatchBlock = true
                });

                CancellationToken cancellationToken = CancellationToken.None;

                // Act
                Task task = product.HandleAsync(context, cancellationToken);

                // Assert
                Assert.NotNull(task);
                task.WaitUntilCompleted();
                Assert.Equal(TaskStatus.RanToCompletion, task.Status);

                Assert.IsType<ExceptionResult>(result);
                ExceptionResult exceptionResult = (ExceptionResult)result;
                Assert.Same(expectedException, exceptionResult.Exception);
                Assert.Equal(includeDetail, exceptionResult.IncludeErrorDetail);
                Assert.Same(expectedContentNegotiator, exceptionResult.ContentNegotiator);
                Assert.Same(expectedRequest, exceptionResult.Request);
                Assert.NotNull(exceptionResult.Formatters);
                Assert.Equal(1, exceptionResult.Formatters.Count());
                Assert.Same(expectedFormatter, exceptionResult.Formatters.Single());
            }
        }

        [Fact]
        public void HandleAsync_IfNotIsTopLevelCatchBlock_LeavesResultNull()
        {
            // Arrange
            Exception exception = CreateDummyException();

            using (HttpConfiguration configuration = CreateConfiguration())
            using (HttpRequestMessage request = CreateRequest())
            {
                ExceptionContext context = new ExceptionContext
                {
                    Exception = exception,
                    RequestContext = new HttpRequestContext
                    {
                        Configuration = configuration
                    },
                    Request = request,
                    IsTopLevelCatchBlock = false
                };

                // More Arrange; then Act & Assert
                TestHandleAsyncLeavesResultNull(context);
            }
        }

        [Fact]
        public void HandleAsync_IfContextIsNull_LeavesResultNull()
        {
            // Arrange
            ExceptionHandlerContext context = null;

            // More Arrange; then Act & Assert
            TestHandleAsyncLeavesResultNull(context);
        }

        [Fact]
        public void HandleAsync_IfExceptionIsNull_LeavesResultNull()
        {
            // Arrange
            Exception exception = null;

            using (HttpConfiguration configuration = CreateConfiguration())
            using (HttpRequestMessage request = CreateRequest())
            {
                ExceptionContext context = new ExceptionContext
                {
                    Exception = exception,
                    RequestContext = new HttpRequestContext
                    {
                        Configuration = configuration
                    },
                    Request = request,
                    IsTopLevelCatchBlock = true
                };

                // More Arrange; then Act & Assert
                TestHandleAsyncLeavesResultNull(context);
            }
        }

        [Fact]
        public void HandleAsync_IfRequestIsNull_LeavesResultNull()
        {
            // Arrange
            Exception exception = CreateDummyException();

            using (HttpConfiguration configuration = CreateConfiguration())
            {
                ExceptionContext context = new ExceptionContext
                {
                    Exception = exception,
                    RequestContext = new HttpRequestContext
                    {
                        Configuration = configuration
                    },
                    Request = null,
                    IsTopLevelCatchBlock = true
                };

                // More Arrange; then Act & Assert
                TestHandleAsyncLeavesResultNull(context);
            }
        }

        [Fact]
        public void HandleAsync_IfRequestContextIsNull_LeavesResultNull()
        {
            // Arrange
            Exception exception = CreateDummyException();

            using (HttpRequestMessage request = CreateRequest())
            {
                ExceptionContext context = new ExceptionContext
                {
                    Exception = exception,
                    RequestContext = null,
                    Request = request,
                    IsTopLevelCatchBlock = true
                };

                // More Arrange; then Act & Assert
                TestHandleAsyncLeavesResultNull(context);
            }
        }

        [Fact]
        public void HandleAsync_IfConfigurationIsNull_LeavesResultNull()
        {
            // Arrange
            Exception exception = CreateDummyException();

            using (HttpRequestMessage request = CreateRequest())
            {
                ExceptionContext context = new ExceptionContext
                {
                    Exception = exception,
                    RequestContext = new HttpRequestContext
                    {
                        Configuration = null
                    },
                    Request = request,
                    IsTopLevelCatchBlock = true
                };

                // More Arrange; then Act & Assert
                TestHandleAsyncLeavesResultNull(context);
            }
        }

        [Fact]
        public void HandleAsync_IfContentNegotiatorIsNull_LeavesResultNull()
        {
            // Arrange
            Exception exception = CreateDummyException();

            using (HttpConfiguration configuration = CreateConfiguration())
            using (HttpRequestMessage request = CreateRequest())
            {
                configuration.Services.Clear(typeof(IContentNegotiator));

                ExceptionContext context = new ExceptionContext
                {
                    Exception = exception,
                    RequestContext = new HttpRequestContext
                    {
                        Configuration = configuration
                    },
                    Request = request,
                    IsTopLevelCatchBlock = true
                };

                // More Arrange; then Act & Assert
                TestHandleAsyncLeavesResultNull(context);
            }
        }

        private static void TestHandleAsyncLeavesResultNull(ExceptionContext context)
        {
            TestHandleAsyncLeavesResultNull(new ExceptionHandlerContext(context));
        }

        private static void TestHandleAsyncLeavesResultNull(ExceptionHandlerContext context)
        {
            Mock<IExceptionHandler> mock = new Mock<IExceptionHandler>(MockBehavior.Strict);
            IHttpActionResult result = null;
            mock
                .Setup(h => h.HandleAsync(It.IsAny<ExceptionHandlerContext>(), It.IsAny<CancellationToken>()))
                .Returns<ExceptionHandlerContext, CancellationToken>((c, i) =>
                {
                    result = c != null ? c.Result : null;
                    return Task.FromResult(0);
                });
            IExceptionHandler innerHander = mock.Object;

            IExceptionHandler product = CreateProductUnderTest(innerHander);

            CancellationToken cancellationToken = CancellationToken.None;

            // Act
            Task task = product.HandleAsync(context, cancellationToken);

            // Assert
            Assert.NotNull(task);
            task.WaitUntilCompleted();
            Assert.Equal(TaskStatus.RanToCompletion, task.Status);

            Assert.Null(result);
        }

        private static CancellationTokenSource CreateCancellationTokenSource()
        {
            return new CancellationTokenSource();
        }

        private static Task CreateCompletedTask()
        {
            TaskCompletionSource<object> source = new TaskCompletionSource<object>();
            source.SetResult(null);
            return source.Task;
        }

        private static HttpConfiguration CreateConfiguration()
        {
            return new HttpConfiguration();
        }

        private static ExceptionHandlerContext CreateContext()
        {
            return new ExceptionHandlerContext(new ExceptionContext());
        }

        private static ExceptionHandlerContext CreateContext(ExceptionContext exceptionContext)
        {
            return new ExceptionHandlerContext(exceptionContext);
        }

        private static IContentNegotiator CreateDummyContentNegotiator()
        {
            return new Mock<IContentNegotiator>(MockBehavior.Strict).Object;
        }

        private static Exception CreateDummyException()
        {
            return new Mock<Exception>(MockBehavior.Strict).Object;
        }

        private static MediaTypeFormatter CreateDummyFormatter()
        {
            return new Mock<MediaTypeFormatter>(MockBehavior.Strict).Object;
        }

        private static IExceptionHandler CreateDummyHandler()
        {
            return new Mock<IExceptionHandler>(MockBehavior.Strict).Object;
        }

        private static LastChanceExceptionHandler CreateProductUnderTest(IExceptionHandler innerHandler)
        {
            return new LastChanceExceptionHandler(innerHandler);
        }

        private static HttpRequestMessage CreateRequest()
        {
            return new HttpRequestMessage();
        }
    }
}
