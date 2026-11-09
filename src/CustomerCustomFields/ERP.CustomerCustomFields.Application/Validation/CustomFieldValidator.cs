using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Domain.Enums;
namespace ERP.CustomerCustomFields.Application.Validation;
public static class CustomFieldValidator
{
 public static void Create(CreateFieldCommand command)
 {
  if(string.IsNullOrWhiteSpace(command.FieldKey)||command.FieldKey.Length>100||string.IsNullOrWhiteSpace(command.Label)||command.Label.Length>200||!Enum.IsDefined(command.FieldType)||command.Options is null)
   throw new CustomFieldException("Use a valid key (100 characters), label (200 characters) and field type.");
  if(command.FieldType==CustomFieldType.Text&&command.Options.Count>0) throw new CustomFieldException("Text fields cannot define options.");
  if(command.Options.Any(o=>o is null||string.IsNullOrWhiteSpace(o.Label)||o.Label.Length>200)) throw new CustomFieldException("Option labels must contain 1 to 200 characters.");
 }
 public static SetFieldValueCommand Value(SetFieldValueCommand command)
 {
  if(command.CustomerId==Guid.Empty||command.FieldId<=0||command.ActorUserId<=0||command.ExpectedVersion is <=0) throw new CustomFieldException("Use valid customer, field, actor and expected version identifiers.");
  if(command.TextValue?.Length>2000) throw new CustomFieldException("Text is limited to 2000 characters.");
  if(command.SelectedOptionIds is {} ids&&(ids.Any(id=>id<=0)||ids.Distinct().Count()!=ids.Count)) throw new CustomFieldException("Selection IDs must be positive and unique.");
  // Only SQL knows the authoritative field type; preserve raw text until that check.
  return command;
 }
}
