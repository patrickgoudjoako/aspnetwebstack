﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.TestCommon;
using Moq;

namespace System.Web.Http.OData.Query.Expressions
{
    public class PropertyContainerTest
    {
        [Fact]
        public void CreatePropertyContainer_CreatesMemberInitExpression()
        {
            // Arrange
            Expression propertyName = Expression.Constant("PropertyName");
            Mock<Expression> propertyValue = new Mock<Expression>();
            propertyValue.Setup(p => p.Type).Returns(typeof(TestEntity));

            var properties = new[] { new NamedPropertyExpression(propertyName, propertyValue.Object) };

            // Act
            Expression container = PropertyContainer.CreatePropertyContainer(properties);

            // Assert
            Assert.Equal(ExpressionType.MemberInit, container.NodeType);
            MemberInitExpression memberInit = container as MemberInitExpression;
            Assert.True(typeof(PropertyContainer).IsAssignableFrom(memberInit.NewExpression.Type));
        }

        [Fact]
        public void CreatePropertyContainer_AutoSelectedProperty()
        {
            // Arrange
            Expression propertyName = Expression.Constant("PropertyName");
            Expression propertyValue = Expression.Constant(42);
            var properties = new[] { new NamedPropertyExpression(propertyName, propertyValue) { AutoSelected = true } };

            // Act
            Expression containerExpression = PropertyContainer.CreatePropertyContainer(properties);

            // Assert
            PropertyContainer container = ToContainer(containerExpression);
            var dict = container.ToDictionary(includeAutoSelected: true);
            Assert.Contains("PropertyName", dict.Keys);

            dict = container.ToDictionary(includeAutoSelected: false);
            Assert.DoesNotContain("PropertyName", dict.Keys);
        }

        [Fact]
        public void CreatePropertyContainer_WithNullCheckTrue_PropertyIsNull()
        {
            // Arrange
            string propertyName = "PropertyName";
            Expression propertyNameExpression = Expression.Constant(propertyName);
            Expression propertyValueExpression = Expression.Constant(42);
            Expression nullCheckExpression = Expression.Constant(true);
            var properties = new[] { new NamedPropertyExpression(propertyNameExpression, propertyValueExpression) { NullCheck = nullCheckExpression } };

            // Act
            Expression containerExpression = PropertyContainer.CreatePropertyContainer(properties);

            // Assert
            PropertyContainer container = ToContainer(containerExpression);
            var dict = container.ToDictionary();
            Assert.Contains(propertyName, dict.Keys);
            Assert.Null(dict[propertyName]);
        }

        [Fact]
        public void CreatePropertyContainer_WithNullCheckFalse_PropertyIsNotNull()
        {
            // Arrange
            string propertyName = "PropertyName";
            int propertyValue = 42;
            Expression propertyNameExpression = Expression.Constant(propertyName);
            Expression propertyValueExpression = Expression.Constant(propertyValue);
            Expression nullCheckExpression = Expression.Constant(false);
            var properties = new[] { new NamedPropertyExpression(propertyNameExpression, propertyValueExpression) { NullCheck = nullCheckExpression } };

            // Act
            Expression containerExpression = PropertyContainer.CreatePropertyContainer(properties);

            // Assert
            PropertyContainer container = ToContainer(containerExpression);
            var dict = container.ToDictionary();
            Assert.Contains(propertyName, dict.Keys);
            Assert.Equal(propertyValue, dict[propertyName]);
        }

        [Fact]
        public void CreatePropertyContainer_MultiplePropertiesWithNullCheck()
        {
            // Arrange
            var properties = new[] 
            { 
                new NamedPropertyExpression(name: Expression.Constant("Prop1"), value: Expression.Constant(1)) { NullCheck = Expression.Constant(true) },
                new NamedPropertyExpression(name: Expression.Constant("Prop2"), value: Expression.Constant(2)) { NullCheck = Expression.Constant(false) },
            };

            // Act
            Expression containerExpression = PropertyContainer.CreatePropertyContainer(properties);

            // Assert
            PropertyContainer container = ToContainer(containerExpression);
            var dict = container.ToDictionary();
            Assert.Null(dict["Prop1"]);
            Assert.Equal(2, dict["Prop2"]);
        }

        [Fact]
        public void CreatePropertyContainer_PageSize()
        {
            // Arrange
            int pageSize = 5;
            Expression propertyName = Expression.Constant("PropertyName");
            Expression propertyValue = Expression.Constant(Enumerable.Range(0, 10));
            var properties = new[] { new NamedPropertyExpression(propertyName, propertyValue) { PageSize = pageSize } };

            // Act
            Expression containerExpression = PropertyContainer.CreatePropertyContainer(properties);

            // Assert
            PropertyContainer container = ToContainer(containerExpression);
            var result = container.ToDictionary()["PropertyName"];
            var truncatedCollection = Assert.IsType<TruncatedCollection<int>>(result);
            Assert.True(truncatedCollection.IsTruncated);
            Assert.Equal(pageSize, truncatedCollection.PageSize);
            Assert.Equal(Enumerable.Range(0, pageSize), truncatedCollection);
        }

        [Fact]
        public void CreatePropertyContainer_WithNullPropertyName_DoesntIncludeTheProperty()
        {
            // Arrange
            Expression propertyName = Expression.Constant(null, typeof(string));
            Expression propertyValue = Expression.Constant(new TestEntity());
            NamedPropertyExpression property = new NamedPropertyExpression(propertyName, propertyValue);
            var properties = new[] { property, property };

            // Act
            Expression containerExpression = PropertyContainer.CreatePropertyContainer(properties);

            // Assert
            PropertyContainer container = ToContainer(containerExpression);
            Assert.Empty(container.ToDictionary(includeAutoSelected: true));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(100)]
        public void CreatePropertyContainer_CreatesPropertyContainer_WithVariousNumberOfProperties(int count)
        {
            // Arrange
            IList<NamedPropertyExpression> properties =
                Enumerable.Range(0, count)
                .Select(i => new NamedPropertyExpression(Expression.Constant(i.ToString()), Expression.Constant(i)))
                .ToList();

            // Act
            Expression containerExpression = PropertyContainer.CreatePropertyContainer(properties);

            // Assert
            Dictionary<string, object> dictionary = ToContainer(containerExpression).ToDictionary(includeAutoSelected: true);
            Assert.Equal(Enumerable.Range(0, count).ToDictionary(i => i.ToString(), i => (object)i).OrderBy(kvp => kvp.Key), dictionary.OrderBy(kvp => kvp.Key));
        }

        private static PropertyContainer ToContainer(Expression containerCreationExpression)
        {
            LambdaExpression containerCreationLambda = Expression.Lambda(containerCreationExpression);
            PropertyContainer container = containerCreationLambda.Compile().DynamicInvoke() as PropertyContainer;
            return container;
        }

        private class TestEntity
        {
        }
    }
}
