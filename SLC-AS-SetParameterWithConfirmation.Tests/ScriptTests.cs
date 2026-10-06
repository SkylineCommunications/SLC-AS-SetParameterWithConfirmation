namespace SLCASSetParameterWithConfirmation.Tests
{
	using System;

	using FluentAssertions;

	using Moq;

	using Xunit;

	public class ScriptInputsTests
	{
		[Theory]
		[InlineData("  [\"Element1\"]  ", "Element1")]
		[InlineData("[\"10/5\"]", "10/5")]
		[InlineData("NoBrackets", "NoBrackets")]
		[InlineData("", "")]
		public void Clean_StripsWhitespaceBracketsAndQuotes(string raw, string expected)
		{
			ScriptInputs.Clean(raw).Should().Be(expected);
		}

		[Fact]
		public void TryParse_ValidInputs_ReturnsTrueAndParsedInputs()
		{
			var reader = new Mock<IScriptInputReader>();
			reader.Setup(r => r.Read(10)).Returns("[\"Element1\"]");
			reader.Setup(r => r.Read(11)).Returns("[\"5\"]");
			reader.Setup(r => r.Read(12)).Returns("[\"NewValue\"]");
			reader.Setup(r => r.Read(13)).Returns("[\"Are you sure?\"]");

			bool result = ScriptInputs.TryParse(reader.Object, out ScriptInputs inputs, out string error);

			result.Should().BeTrue();
			error.Should().BeNull();
			inputs.ElementIdentifier.Should().Be("Element1");
			inputs.ParameterId.Should().Be(5);
			inputs.Value.Should().Be("NewValue");
			inputs.ConfirmationMessage.Should().Be("Are you sure?");
		}

		[Fact]
		public void TryParse_EmptyValueParameter_IsAllowed()
		{
			var reader = new Mock<IScriptInputReader>();
			reader.Setup(r => r.Read(10)).Returns("Element1");
			reader.Setup(r => r.Read(11)).Returns("5");
			reader.Setup(r => r.Read(12)).Returns("");
			reader.Setup(r => r.Read(13)).Returns("Are you sure?");

			bool result = ScriptInputs.TryParse(reader.Object, out ScriptInputs inputs, out string error);

			result.Should().BeTrue();
			inputs.Value.Should().Be(String.Empty);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		public void TryParse_WhitespaceElementIdentifier_ReturnsFalseWithError(string rawElementIdentifier)
		{
			var reader = new Mock<IScriptInputReader>();
			reader.Setup(r => r.Read(10)).Returns(rawElementIdentifier);
			reader.Setup(r => r.Read(11)).Returns("5");
			reader.Setup(r => r.Read(12)).Returns("value");
			reader.Setup(r => r.Read(13)).Returns("Are you sure?");

			bool result = ScriptInputs.TryParse(reader.Object, out ScriptInputs inputs, out string error);

			result.Should().BeFalse();
			inputs.Should().BeNull();
			error.Should().Contain("Invalid Element Identifier");
		}

		[Theory]
		[InlineData("")]
		[InlineData("not-a-number")]
		public void TryParse_InvalidParameterId_ReturnsFalseWithError(string rawParamId)
		{
			var reader = new Mock<IScriptInputReader>();
			reader.Setup(r => r.Read(10)).Returns("Element1");
			reader.Setup(r => r.Read(11)).Returns(rawParamId);
			reader.Setup(r => r.Read(12)).Returns("value");
			reader.Setup(r => r.Read(13)).Returns("Are you sure?");

			bool result = ScriptInputs.TryParse(reader.Object, out ScriptInputs inputs, out string error);

			result.Should().BeFalse();
			inputs.Should().BeNull();
			error.Should().Contain("Invalid Parameter ID");
		}

		[Fact]
		public void TryParse_WhitespaceConfirmationMessage_ReturnsFalseWithError()
		{
			var reader = new Mock<IScriptInputReader>();
			reader.Setup(r => r.Read(10)).Returns("Element1");
			reader.Setup(r => r.Read(11)).Returns("5");
			reader.Setup(r => r.Read(12)).Returns("value");
			reader.Setup(r => r.Read(13)).Returns("   ");

			bool result = ScriptInputs.TryParse(reader.Object, out ScriptInputs inputs, out string error);

			result.Should().BeFalse();
			inputs.Should().BeNull();
			error.Should().Contain("Invalid Confirmation Message");
		}
	}

	public class ParameterWriteGateTests
	{
		[Fact]
		public void Execute_Confirmed_CallsSetParameter()
		{
			var writer = new Mock<IElementParameterWriter>();

			ParameterWriteGate.Execute(writer.Object, 5, "NewValue", confirmed: true);

			writer.Verify(w => w.SetParameter(5, "NewValue"), Times.Once);
		}

		[Fact]
		public void Execute_NotConfirmed_NeverCallsSetParameter()
		{
			var writer = new Mock<IElementParameterWriter>();

			ParameterWriteGate.Execute(writer.Object, 5, "NewValue", confirmed: false);

			writer.Verify(w => w.SetParameter(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
		}
	}

	public class EngineScriptInputReaderTests
	{
		[Fact]
		public void Constructor_NullEngine_Throws()
		{
			Action act = () => new EngineScriptInputReader(null);

			act.Should().Throw<ArgumentNullException>();
		}
	}

	public class ElementParameterWriterTests
	{
		[Fact]
		public void Constructor_NullElement_Throws()
		{
			Action act = () => new ElementParameterWriter(null);

			act.Should().Throw<ArgumentNullException>();
		}
	}
}
