using FluentValidation;
using ServiceScheduler.Api.Models;

namespace ServiceScheduler.Api.Validators;

public class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.DealershipLocation).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdvisorId).NotEmpty().MaximumLength(100);

        RuleFor(x => x.ServiceTypeIds)
            .NotEmpty().WithMessage("At least one service type is required.")
            .Must(ids => ids.TrueForAll(id => id > 0)).WithMessage("All service type IDs must be positive.");

        // reject bookings in the past
        RuleFor(x => x.StartTime)
            .GreaterThan(DateTime.UtcNow).WithMessage("StartTime must be in the future.");
    }
}
