using VetFlow.Domain.Common;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;
using VetFlow.Domain.Services;

namespace VetFlow.Tests;

public class AppointmentSchedulerTests
{
    // Wednesday 21 Oct 2026, 08:00 – before opening time.
    private static readonly DateTime Now = new(2026, 10, 21, 8, 0, 0);
    private static readonly DateTime Thursday = new(2026, 10, 22);
    private static readonly ClinicSchedule Schedule = ClinicSchedule.Default;

    private static Appointment Booked(int id, DateTime start, int minutes, AppointmentStatus? status = null)
    {
        var a = new Appointment { Id = 0, PatientId = 1, VeterinarianId = 1 };
        a.SetTime(start, minutes);
        a.Id = id;
        if (status == AppointmentStatus.Cancelled) a.Cancel("test");
        return a;
    }

    [Fact]
    public void Valid_booking_has_no_errors() =>
        Assert.Empty(AppointmentScheduler.Validate(Thursday.AddHours(10), 30, [], Schedule, Now));

    [Fact]
    public void Overlapping_booking_is_rejected()
    {
        var existing = Booked(1, Thursday.AddHours(10), 30);

        var errors = AppointmentScheduler.Validate(Thursday.AddHours(10).AddMinutes(15), 30, [existing], Schedule, Now);

        Assert.Contains(errors, e => e.Contains("10:00 to 10:30"));
    }

    [Fact]
    public void Back_to_back_bookings_are_allowed()
    {
        var existing = Booked(1, Thursday.AddHours(10), 30);
        Assert.Empty(AppointmentScheduler.Validate(Thursday.AddHours(10).AddMinutes(30), 15, [existing], Schedule, Now));
    }

    [Fact]
    public void Cancelled_appointment_frees_its_slot()
    {
        var cancelled = Booked(1, Thursday.AddHours(10), 30, AppointmentStatus.Cancelled);
        Assert.Empty(AppointmentScheduler.Validate(Thursday.AddHours(10), 30, [cancelled], Schedule, Now));
    }

    [Fact]
    public void Rescheduling_ignores_the_appointment_itself()
    {
        var self = Booked(7, Thursday.AddHours(10), 30);
        Assert.Empty(AppointmentScheduler.Validate(Thursday.AddHours(10).AddMinutes(15), 30, [self], Schedule, Now, ignoreAppointmentId: 7));
    }

    [Theory]
    [InlineData(8, 30, 30)]   // before opening
    [InlineData(18, 45, 30)]  // ends after closing
    public void Outside_opening_hours_is_rejected(int hour, int minute, int duration) =>
        Assert.Contains(AppointmentScheduler.Validate(Thursday.AddHours(hour).AddMinutes(minute), duration, [], Schedule, Now),
            e => e.Contains("09:00 and 19:00"));

    [Fact]
    public void Sunday_and_past_are_rejected()
    {
        Assert.Contains(AppointmentScheduler.Validate(new DateTime(2026, 10, 25, 10, 0, 0), 30, [], Schedule, Now), e => e.Contains("closed"));
        Assert.Contains(AppointmentScheduler.Validate(new DateTime(2026, 10, 20, 10, 0, 0), 30, [], Schedule, Now), e => e.Contains("past"));
    }

    [Fact]
    public void Available_slots_skip_booked_time()
    {
        var existing = Booked(1, Thursday.AddHours(9), 60);

        var slots = AppointmentScheduler.AvailableSlots(Thursday, 30, [existing], Schedule, Now);

        Assert.Equal(Thursday.AddHours(10), slots[0]);
        Assert.Equal(Thursday.AddHours(18).AddMinutes(30), slots[^1]);
        Assert.DoesNotContain(Thursday.AddHours(9).AddMinutes(45), slots);
    }
}

public class AppointmentWorkflowTests
{
    private static readonly DateTime Start = new(2026, 10, 22, 10, 0, 0);

    private static Appointment New()
    {
        var a = new Appointment();
        a.SetTime(Start, 30);
        return a;
    }

    [Fact]
    public void Check_in_start_complete()
    {
        var a = New();
        a.CheckIn(Start.AddMinutes(-10));
        a.Start();
        a.Complete();
        Assert.Equal(AppointmentStatus.Completed, a.Status);
        Assert.Equal(Start.AddMinutes(-10), a.CheckedInAt);
    }

    [Fact]
    public void No_show_only_after_start_time()
    {
        var a = New();
        Assert.Throws<DomainException>(() => a.MarkNoShow(Start.AddMinutes(-1)));
        a.MarkNoShow(Start.AddMinutes(20));
        Assert.Equal(AppointmentStatus.NoShow, a.Status);
        Assert.False(a.BlocksCalendar);
    }

    [Fact]
    public void Completed_appointment_cannot_be_cancelled()
    {
        var a = New();
        a.Start();
        a.Complete();
        Assert.Throws<DomainException>(() => a.Cancel("late"));
    }
}
