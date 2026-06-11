using Cedeva.Core.Entities;
using Cedeva.Infrastructure.Services;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Services;

public class BookingQuestionServiceTests
{
    private static (SqliteTestContext db, Activity activity, Booking booking) SeedBase()
    {
        var db = new SqliteTestContext();
        var org = TestData.Organisation();
        var activity = TestData.Activity(org);
        var parent = TestData.Parent(org);
        var child = TestData.Child(parent);
        var booking = TestData.Booking(child, activity, group: null, totalAmount: 100m, paidAmount: 0m);

        db.Context.AddRange(org, activity, parent, child, booking);
        db.Context.SaveChanges();
        return (db, activity, booking);
    }

    [Fact]
    public async Task SaveAnswers_PersistsNonEmptyAndSkipsBlank()
    {
        var (db, activity, booking) = SeedBase();
        using var _ = db;
        var q1 = TestData.Question(activity, "Allergies?");
        var q2 = TestData.Question(activity, "Notes?");
        db.Context.AddRange(q1, q2);
        db.Context.SaveChanges();
        var sut = new BookingQuestionService(db.Context);

        await sut.SaveAnswersAsync(booking.Id, new Dictionary<int, string>
        {
            [q1.Id] = "Aucune",
            [q2.Id] = "   " // blank -> skipped
        });

        await using var verify = db.NewContext();
        var answers = await verify.ActivityQuestionAnswers.Where(a => a.BookingId == booking.Id).ToListAsync();
        answers.Should().ContainSingle();
        answers[0].ActivityQuestionId.Should().Be(q1.Id);
        answers[0].AnswerText.Should().Be("Aucune");
    }

    [Fact]
    public async Task SaveAnswers_ReplacesPreviousAnswers()
    {
        var (db, activity, booking) = SeedBase();
        using var _ = db;
        var q1 = TestData.Question(activity, "Q1");
        var q2 = TestData.Question(activity, "Q2");
        db.Context.AddRange(q1, q2);
        db.Context.SaveChanges();
        var sut = new BookingQuestionService(db.Context);

        await sut.SaveAnswersAsync(booking.Id, new Dictionary<int, string> { [q1.Id] = "first" });
        await sut.SaveAnswersAsync(booking.Id, new Dictionary<int, string> { [q2.Id] = "second" });

        await using var verify = db.NewContext();
        var answers = await verify.ActivityQuestionAnswers.Where(a => a.BookingId == booking.Id).ToListAsync();
        answers.Should().ContainSingle();
        answers[0].ActivityQuestionId.Should().Be(q2.Id);
        answers[0].AnswerText.Should().Be("second");
    }

    [Fact]
    public async Task GetQuestionsWithAnswers_IncludesInactiveOnlyWhenAnswered_AndMapsAnswerText()
    {
        var (db, activity, booking) = SeedBase();
        using var _ = db;
        var qActive = TestData.Question(activity, "Active", isActive: true, displayOrder: 1);
        var qInactiveAnswered = TestData.Question(activity, "InactiveAnswered", isActive: false, displayOrder: 2);
        var qInactiveUnanswered = TestData.Question(activity, "InactiveUnanswered", isActive: false, displayOrder: 3);
        db.Context.AddRange(qActive, qInactiveAnswered, qInactiveUnanswered);
        db.Context.SaveChanges();

        var sut = new BookingQuestionService(db.Context);
        await sut.SaveAnswersAsync(booking.Id, new Dictionary<int, string> { [qInactiveAnswered.Id] = "kept" });

        var result = await sut.GetQuestionsWithAnswersAsync(activity.Id, booking.Id);

        result.Select(q => q.Id).Should().Equal(qActive.Id, qInactiveAnswered.Id); // ordered, no unanswered-inactive
        result.Single(q => q.Id == qInactiveAnswered.Id).AnswerText.Should().Be("kept");
        result.Single(q => q.Id == qActive.Id).AnswerText.Should().BeNull();
    }

    [Fact]
    public async Task ValidateRequiredQuestions_TrueWhenAllRequiredAnswered()
    {
        var (db, activity, _) = SeedBase();
        using var _ = db;
        var required = TestData.Question(activity, "Required", isRequired: true);
        var optional = TestData.Question(activity, "Optional", isRequired: false);
        db.Context.AddRange(required, optional);
        db.Context.SaveChanges();
        var sut = new BookingQuestionService(db.Context);

        var ok = await sut.ValidateRequiredQuestionsAsync(activity.Id,
            new Dictionary<int, string> { [required.Id] = "answer" });

        ok.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateRequiredQuestions_FalseWhenRequiredMissingOrBlank(string blank)
    {
        var (db, activity, _) = SeedBase();
        using var _ = db;
        var required = TestData.Question(activity, "Required", isRequired: true);
        db.Context.Add(required);
        db.Context.SaveChanges();
        var sut = new BookingQuestionService(db.Context);

        (await sut.ValidateRequiredQuestionsAsync(activity.Id, new Dictionary<int, string>()))
            .Should().BeFalse();
        (await sut.ValidateRequiredQuestionsAsync(activity.Id, new Dictionary<int, string> { [required.Id] = blank }))
            .Should().BeFalse();
    }
}
