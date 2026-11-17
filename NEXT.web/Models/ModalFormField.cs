namespace NEXT.web.Models;

public sealed record ModalFormField(FormField Field, IReadOnlyList<FormField> AllFields, bool ProductPricing);
