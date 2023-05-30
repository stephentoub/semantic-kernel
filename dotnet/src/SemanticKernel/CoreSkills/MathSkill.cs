// Copyright (c) Microsoft. All rights reserved.

using System;
using System.ComponentModel;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Microsoft.SemanticKernel.CoreSkills;

/// <summary>
/// MathSkill provides a set of functions to make Math calculations.
/// </summary>
/// <example>
/// Usage: kernel.ImportSkill("math", new MathSkill());
/// Examples:
/// {{math.Add}}         => Returns the sum of FirstNumber and SecondNumber (provided in the SKContext)
/// </example>
public sealed class MathSkill
{
    /// <summary>
    /// Returns the Addition result of initial and amount values provided.
    /// </summary>
    /// <param name="value">Initial value to which to add the specified amount</param>
    /// <param name="amount">The amount to add as a string.</param>
    /// <returns>The resulting sum as a string.</returns>
    [SKFunction, Description("Adds an amount to a value")]
    public int Add(
        [SKName("input"), Description("The value to add")] int value,
        [Description("Amount to add")] int amount) =>
        value + amount;

    /// <summary>
    /// Returns the Sum of two SKContext numbers provided.
    /// </summary>
    /// <param name="value">Initial value from which to subtract the specified amount</param>
    /// <param name="amount">The amount to subtract as a string.</param>
    /// <returns>The resulting subtraction as a string.</returns>
    [SKFunction, Description("Subtracts an amount from a value")]
    public int Subtract(
        [SKName("input"), Description("The value to subtract")] int value,
        [Description("Amount to subtract")] int amount) =>
        value - amount;

    // ----

    [SKFunction(), Description("Returns the absolute value of a specified number")]
    public double Abs(
        [Description("The number for which to return the absolute value")] double input) =>
        Math.Abs(input);

    [SKFunction, Description("Returns the angle whose cosine is the specified number")]
    public double Acos(
        [Description("A number representing a cosine, where the number must be greater than or equal to -1, but less than or equal to 1")] double input) =>
        Math.Acos(input);

    [SKFunction, Description("Returns the angle whose sine is the specified number")]
    public double Asin(
        [Description("A number representing a sine, where the number must be greater than or equal to -1, but less than or equal to 1")] double input) =>
        Math.Asin(input);

    [SKFunction, Description("Returns the angle, measured in radians, whose tangent is the specified number")]
    public double Atan(
        [Description("A number representing a tangent")] double input) =>
        Math.Atan(input);

    [SKFunction, Description("Returns the angle whose tangent is the quotient of two specified numbers")]
    public double Atan2(
        [Description("The y coordinate of a point")] double y,
        [Description("The x coordinate of a point")] double x) =>
        Math.Atan2(y, x);

    [SKFunction, Description("Returns the cosine of the specified angle")]
    public double Cos(
        [Description("An angle, measured in radians.")] double input) =>
        Math.Cos(input);

    [SKFunction, Description("Returns the hyperbolic cosine of the specified angle")]
    public double Cosh(
        [Description("An angle, measured in radians.")] double input) =>
        Math.Cosh(input);

    [SKFunction, Description("Returns e raised to the specified power")]
    public double Exp(
        [Description("A number specifying a power")] double input) =>
        Math.Exp(input);

    [SKFunction, Description("Returns the remainder resulting from the division of a specified number by another specified number")]
    public double IEEERemainder(
        [Description("A number specifying a power")] double input) =>
        Math.Exp(input);

    // Only available on .NET Core:
    // Acosh
    // Asinh
    // Atanh
    // Cbrt
    // CopySign

}
