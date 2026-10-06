# Set Parameter With Confirmation

## About

This Automation script sets a parameter value on a DataMiner element, but only after an operator **explicitly confirms the change**. Before writing anything, the script shows an interactive dialog with a configurable confirmation message and **Yes**/**No** buttons.

This makes it a safer alternative to a plain "set parameter" script for actions that are sensitive, hard to undo, or that should always involve a human decision point, for example changing a device mode, toggling a critical switch, or adjusting a setting that affects live traffic. If the operator presses **No**, closes or detaches from the dialog, lets the script time out, or if anything goes wrong before the confirmation, **no write to the element ever happens**.

## Key Features

- **Confirm before writing**: shows a Yes/No dialog with a custom message before setting the parameter.
- **Fail-safe by default**: any outcome other than an explicit "Yes" (No, closing the dialog, detaching, aborting, or a timeout) leaves the element untouched.
- **Flexible element lookup**: accepts either an element name or a `DataMinerID/ElementID` key.
- **Works with any string-settable parameter**: pass any parameter ID and value supported by the target element.
- **Table parameter support**: optionally supply a row index/display key to set a single cell in a table parameter instead of a regular parameter.

## Prerequisites

- DataMiner version **10.6.0** or higher (interactive dialogs require the DataMiner Automation IAS runtime available from this version onward).
- The target element must exist and be active on the DataMiner System.

## Parameters

| ID | Name | Description |
|----|------|--------------|
| 10 | Element Identifier | The element to update, either by name or by `DataMinerID/ElementID` key. |
| 11 | Parameter Identifier | The numeric ID of the parameter to set. |
| 12 | Value | The value to write to the parameter. May be empty. |
| 13 | ConfirmationMessage | The message shown to the operator in the confirmation dialog. |
| 14 | Index | Optional row display key for a table parameter. Leave empty or set to `null` to perform a regular, non-table parameter set. |