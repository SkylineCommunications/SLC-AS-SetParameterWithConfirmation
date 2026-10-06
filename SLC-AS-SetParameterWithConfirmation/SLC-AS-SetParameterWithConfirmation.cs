namespace SLCASSetParameterWithConfirmation
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess).
				// No write can have happened at this point unless the user already confirmed it explicitly.
				throw;
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				throw;
			}
			catch (Exception ex)
			{
				// Never surface the raw exception details of potentially sensitive script inputs; log the full
				// exception for troubleshooting and return a generic failure message to the caller.
				engine.Log("Run|Unexpected exception: " + ex.ToString());
				engine.ExitFail("Runtime operation failed.");
			}
		}

		private static void RunSafe(IEngine engine)
		{
			var reader = new EngineScriptInputReader(engine);

			// Read and validate every input first. Nothing is written to the element until the user explicitly
			// confirms the action in the dialog below.
			if (!ScriptInputs.TryParse(reader, out ScriptInputs inputs, out string validationError))
			{
				engine.ExitFail(validationError);
				return;
			}

			Element element = ResolveElement(engine, inputs.ElementIdentifier);
			if (element == null)
			{
				engine.ExitFail($"Element not found: '{inputs.ElementIdentifier}'");
				return;
			}

			if (!element.IsActive)
			{
				engine.ExitFail($"Element is not active: '{inputs.ElementIdentifier}'");
				return;
			}

			var controller = new InteractiveController(engine);
			var dialog = new ConfirmationDialog(engine, inputs.ConfirmationMessage);

			// The write is strictly gated on the user explicitly pressing "Yes". Pressing "No", closing/detaching
			// from the dialog, an abort, a timeout, or any UI failure all skip this handler entirely, so no write
			// ever happens in those cases.
			dialog.YesButton.Pressed += (sender, args) =>
			{
				ParameterWriteGate.Execute(new ElementParameterWriter(element), inputs.ParameterId, inputs.Value, confirmed: true);
				engine.ExitSuccess("Parameter set.");
			};
			dialog.NoButton.Pressed += (sender, args) =>
			{
				engine.ExitSuccess("Canceled by user. No changes were made.");
			};

			controller.ShowDialog(dialog);
		}

		private static Element ResolveElement(IEngine engine, string elementIdentifier)
		{
			// If the identifier contains a '/', we assume it's in the format "DataMinerID/ElementID".
			// If the identifier does not contain a '/', we assume it's an element name and try to get the element directly.
			return elementIdentifier.Contains("/")
				? engine.FindElementByKey(elementIdentifier)
				: engine.FindElement(elementIdentifier);
		}
	}

	/// <summary>
	/// Reads raw script input values from the engine. Kept as a thin seam so parsing/validation logic can be
	/// unit tested without depending on the SDK-owned <see cref="ScriptParam"/> type.
	/// </summary>
	public interface IScriptInputReader
	{
		/// <summary>
		/// Reads the raw value of the script parameter with the given id.
		/// </summary>
		/// <param name="id">The script parameter id.</param>
		/// <returns>The raw script parameter value.</returns>
		string Read(int id);
	}

	/// <summary>
	/// Production <see cref="IScriptInputReader"/> implementation backed by the real <see cref="IEngine"/>.
	/// </summary>
	public sealed class EngineScriptInputReader : IScriptInputReader
	{
		private readonly IEngine engine;

		/// <summary>
		/// Initializes a new instance of the <see cref="EngineScriptInputReader"/> class.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public EngineScriptInputReader(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
		}

		/// <inheritdoc/>
		public string Read(int id)
		{
			return engine.GetScriptParam(id).Value;
		}
	}

	/// <summary>
	/// Writes a parameter value to an element. Kept as a thin seam because <see cref="Element"/> is a concrete SDK
	/// type that is awkward to mock directly.
	/// </summary>
	public interface IElementParameterWriter
	{
		/// <summary>
		/// Sets the given parameter to the given value on the wrapped element.
		/// </summary>
		/// <param name="parameterId">The parameter id.</param>
		/// <param name="value">The value to set.</param>
		void SetParameter(int parameterId, string value);
	}

	/// <summary>
	/// Production <see cref="IElementParameterWriter"/> implementation backed by a real <see cref="Element"/>.
	/// </summary>
	public sealed class ElementParameterWriter : IElementParameterWriter
	{
		private readonly Element element;

		/// <summary>
		/// Initializes a new instance of the <see cref="ElementParameterWriter"/> class.
		/// </summary>
		/// <param name="element">The element to write the parameter to.</param>
		public ElementParameterWriter(Element element)
		{
			this.element = element ?? throw new ArgumentNullException(nameof(element));
		}

		/// <inheritdoc/>
		public void SetParameter(int parameterId, string value)
		{
			element.SetParameter(parameterId, value);
		}
	}

	/// <summary>
	/// Gates the actual parameter write strictly on explicit user confirmation.
	/// </summary>
	public static class ParameterWriteGate
	{
		/// <summary>
		/// Writes the parameter value through <paramref name="writer"/> only when <paramref name="confirmed"/> is
		/// <see langword="true"/>. No write of any kind happens otherwise.
		/// </summary>
		/// <param name="writer">The destination to write the parameter value to.</param>
		/// <param name="parameterId">The parameter id to write.</param>
		/// <param name="value">The value to write.</param>
		/// <param name="confirmed">Whether the user explicitly confirmed the write.</param>
		public static void Execute(IElementParameterWriter writer, int parameterId, string value, bool confirmed)
		{
			if (!confirmed)
			{
				return;
			}

			writer.SetParameter(parameterId, value);
		}
	}

	/// <summary>
	/// Holds the parsed and validated script inputs.
	/// </summary>
	public sealed class ScriptInputs
	{
		private ScriptInputs(string elementIdentifier, int parameterId, string value, string confirmationMessage)
		{
			ElementIdentifier = elementIdentifier;
			ParameterId = parameterId;
			Value = value;
			ConfirmationMessage = confirmationMessage;
		}

		/// <summary>
		/// Gets the cleaned-up element identifier (either an element name or a "DataMinerID/ElementID" key).
		/// </summary>
		public string ElementIdentifier { get; }

		/// <summary>
		/// Gets the parsed parameter id to set.
		/// </summary>
		public int ParameterId { get; }

		/// <summary>
		/// Gets the value to set. May be empty.
		/// </summary>
		public string Value { get; }

		/// <summary>
		/// Gets the message to display to the user in the confirmation dialog.
		/// </summary>
		public string ConfirmationMessage { get; }

		/// <summary>
		/// Cleans up a raw script parameter value the same way the original SetParameter script does:
		/// trims whitespace and strips surrounding '[', '"' and ']' characters left over from array-style input.
		/// </summary>
		/// <param name="rawValue">The raw script parameter value.</param>
		/// <returns>The cleaned-up value.</returns>
		public static string Clean(string rawValue)
		{
			return (rawValue ?? String.Empty).Trim().TrimStart('[', '"').TrimEnd('"', ']');
		}

		/// <summary>
		/// Reads and validates all script inputs without causing any side effects.
		/// </summary>
		/// <param name="reader">The reader used to read the raw script parameter values.</param>
		/// <param name="inputs">The parsed inputs when parsing succeeds.</param>
		/// <param name="error">The <c>ExitFail</c> message when parsing fails.</param>
		/// <returns><see langword="true"/> when every input is valid; otherwise <see langword="false"/>.</returns>
		public static bool TryParse(IScriptInputReader reader, out ScriptInputs inputs, out string error)
		{
			if (reader == null)
			{
				throw new ArgumentNullException(nameof(reader));
			}

			var elementIdentifier = Clean(reader.Read(10));
			if (String.IsNullOrWhiteSpace(elementIdentifier))
			{
				inputs = null;
				error = $"Invalid Element Identifier: '{elementIdentifier}'";
				return false;
			}

			var paramIdString = Clean(reader.Read(11));
			if (String.IsNullOrWhiteSpace(paramIdString) || !Int32.TryParse(paramIdString, out int parameterId))
			{
				inputs = null;
				error = $"Invalid Parameter ID: '{paramIdString}'";
				return false;
			}

			// The value is allowed to be empty.
			var value = Clean(reader.Read(12));

			var confirmationMessage = Clean(reader.Read(13));
			if (String.IsNullOrWhiteSpace(confirmationMessage))
			{
				inputs = null;
				error = "Invalid Confirmation Message: value cannot be empty.";
				return false;
			}

			inputs = new ScriptInputs(elementIdentifier, parameterId, value, confirmationMessage);
			error = null;
			return true;
		}
	}

	/// <summary>
	/// A simple Yes/No confirmation dialog showing the configured confirmation message.
	/// </summary>
	internal class ConfirmationDialog : Dialog
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConfirmationDialog"/> class.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		/// <param name="message">The confirmation message to display to the user.</param>
		public ConfirmationDialog(IEngine engine, string message)
			: base(engine)
		{
			Title = "Confirm Parameter Change";

			AddWidget(new Label(message), 0, 0, 1, 2);
			AddWidget(YesButton, 1, 0);
			AddWidget(NoButton, 1, 1);
		}

		/// <summary>
		/// Gets the button that confirms the parameter write.
		/// </summary>
		public Button YesButton { get; } = new Button("Yes");

		/// <summary>
		/// Gets the button that cancels the parameter write.
		/// </summary>
		public Button NoButton { get; } = new Button("No");
	}
}
