// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Xunit;

namespace SemanticKernel.UnitTests.Functions;

public class ParameterViewTypeTests
{
    [Theory]
    [InlineData("string")]
    [InlineData("number")]
    [InlineData("object")]
    [InlineData("array")]
    [InlineData("boolean")]
    public void ItCanConvertParameterDataTypeToString(string name)
    {
        //Arrange
        var sut = new ParameterViewJsonType(name);

        //Act
        var result = sut.ToString();

        //Assert
        Assert.Equal(name, result);
    }

    [Fact]
    public void ItCanCreateStringParameterDataType()
    {
        //Act
        var sut = ParameterViewJsonType.String;

        //Assert
        Assert.Equal("string", sut.Name);
    }

    [Fact]
    public void ItCanCreateNumberParameterDataType()
    {
        //Act
        var sut = ParameterViewJsonType.Number;

        //Assert
        Assert.Equal("number", sut.Name);
    }

    [Fact]
    public void ItCanCreateObjectParameterDataType()
    {
        //Act
        var sut = ParameterViewJsonType.Object;

        //Assert
        Assert.Equal("object", sut.Name);
    }

    [Fact]
    public void ItCanArrayParameterDataType()
    {
        //Act
        var sut = ParameterViewJsonType.Array;

        //Assert
        Assert.Equal("array", sut.Name);
    }

    [Fact]
    public void ItCanCreateBooleanParameterDataType()
    {
        //Act
        var sut = ParameterViewJsonType.Boolean;

        //Assert
        Assert.Equal("boolean", sut.Name);
    }

    [Fact]
    public void ItCanCheckTwoParameterDataTypesAreEqual()
    {
        //Arrange
        var sut1 = new ParameterViewJsonType("array");
        var sut2 = new ParameterViewJsonType("array");

        //Assert
        Assert.True(sut1.Equals(sut2));
    }

    [Fact]
    public void ItCanCheckTwoParameterDataTypesAreUnequal()
    {
        //Arrange
        var sut1 = new ParameterViewJsonType("array");
        var sut2 = new ParameterViewJsonType("string");

        //Assert
        Assert.False(sut1.Equals(sut2));
    }

    [Fact]
    public void ItCanCheckParameterDataTypeIsEqualToAnotherOneRepresentedByObject()
    {
        //Arrange
        var sut1 = new ParameterViewJsonType("array");
        object sut2 = new ParameterViewJsonType("array");

        //Assert
        Assert.True(sut1.Equals(sut2));
    }

    [Fact]
    public void ItCanCheckParameterDataTypeIsUnequalToAnotherOneRepresentedByObject()
    {
        //Arrange
        var sut1 = new ParameterViewJsonType("array");
        object sut2 = new ParameterViewJsonType("string");

        //Assert
        Assert.False(sut1.Equals(sut2));
    }

    [Fact]
    public void ItCanCheckParameterDataTypeIsUnequalToAnotherType()
    {
        //Arrange
        var sut1 = new ParameterViewJsonType("array");
        var sut2 = "array";

        //Assert
        Assert.False(sut1.Equals(sut2));
    }
}
