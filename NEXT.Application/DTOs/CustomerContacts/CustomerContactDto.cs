namespace NEXT.Application.DTOs.CustomerContacts;

public sealed class CustomerContactDto : CustomerContactInputDto, NEXT.Application.DTOs.Common.IRecordDto
{
    public Guid Id { get; set; }

}
