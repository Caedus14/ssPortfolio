using Portfolio.Api.Contact;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class ContactValidatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Name_length_boundaries_pass(int length)
    {
        ContactValidationResult result = Validate(name: new string('A', length));

        Assert.True(result.IsValid);
        Assert.Equal(new string('A', length), result.Contact!.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Name_length_boundaries_fail(int length)
    {
        ContactValidationResult result = Validate(name: new string('A', length));

        Assert.Equal("Name must be 1 to 100 characters.", result.FieldErrors["name"]);
    }

    [Fact]
    public void Name_is_trimmed_and_rejects_control_characters()
    {
        ContactValidationResult trimmed = Validate(name: "  Ada  ");
        Assert.Equal("Ada", trimmed.Contact!.Name);

        ContactValidationResult control = Validate(name: "Ada\nLovelace");
        Assert.Equal("Name cannot include control characters.", control.FieldErrors["name"]);
    }

    [Fact]
    public void A_plausible_email_passes_and_keeps_its_case()
    {
        ContactValidationResult result = Validate(email: "  Ada@Example.COM  ");

        Assert.True(result.IsValid);
        Assert.Equal("Ada@Example.COM", result.Contact!.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("a@b")]
    [InlineData("@example.com")]
    [InlineData("ada@")]
    [InlineData("ada@@example.com")]
    [InlineData("ada@example")]
    [InlineData("ada@.example.com")]
    [InlineData("ada@example.com.")]
    [InlineData("ada@ex ample.com")]
    [InlineData("ada@example..com")]
    public void A_bad_email_has_one_field_message(string email)
    {
        ContactValidationResult result = Validate(email: email);

        Assert.Equal(
            "Enter an email address, up to 254 characters, without spaces.",
            result.FieldErrors["email"]
        );
    }

    [Fact]
    public void Email_length_is_254()
    {
        string ok = "a@b." + new string('c', 250);
        Assert.Equal(254, ok.Length);
        Assert.True(Validate(email: ok).IsValid);

        string tooLong = ok + "c";
        Assert.Equal(255, tooLong.Length);
        Assert.False(Validate(email: tooLong).IsValid);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(2000)]
    public void Message_length_boundaries_pass(int length)
    {
        ContactValidationResult result = Validate(message: new string('m', length));

        Assert.True(result.IsValid);
        Assert.Equal(length, result.Contact!.Message.Length);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(2001)]
    public void Message_length_boundaries_fail(int length)
    {
        ContactValidationResult result = Validate(message: new string('m', length));

        Assert.Equal("Message must be 10 to 2000 characters.", result.FieldErrors["message"]);
    }

    [Fact]
    public void Message_allows_newlines_and_rejects_other_controls()
    {
        Assert.True(Validate(message: "hello\r\n\tthere").IsValid);

        ContactValidationResult control = Validate(message: "hello\u0001world");
        Assert.Equal("Message cannot include control characters.", control.FieldErrors["message"]);
    }

    [Theory]
    [InlineData("hello <b>there")]
    [InlineData("hello </div> there")]
    [InlineData("hello <script src=\"x\">")]
    [InlineData("hello <!-- comment -->")]
    [InlineData("hello <?xml version=\"1\"?>")]
    public void Html_tags_are_rejected(string message)
    {
        ContactValidationResult result = Validate(message: message);

        Assert.Equal("Message cannot include HTML.", result.FieldErrors["message"]);
    }

    [Theory]
    [InlineData("a < b is fine")]
    [InlineData("5 < 10 is fine!!")]
    [InlineData("see < the note")]
    public void A_less_than_sign_with_a_space_is_allowed(string message)
    {
        Assert.True(Validate(message: message).IsValid);
    }

    [Fact]
    public void Two_links_pass_and_three_fail()
    {
        Assert.True(Validate(message: "See https://a.example and https://b.example please.").IsValid);
        Assert.True(Validate(message: "See https://www.example.com for the one link.").IsValid);
        Assert.Equal(1, ContactValidator.CountLinks("HTTPS://WWW.Example.COM/docs"));

        ContactValidationResult three = Validate(
            message: "https://a.example http://b.example www.c.example"
        );
        Assert.Equal("Message can include at most 2 links.", three.FieldErrors["message"]);
    }

    [Fact]
    public void Html_is_reported_before_an_extra_link()
    {
        ContactValidationResult result = Validate(
            message: "<b>https://a.example https://b.example https://c.example</b>"
        );

        Assert.Equal("Message cannot include HTML.", result.FieldErrors["message"]);
    }

    [Fact]
    public void Every_failing_field_is_returned_together()
    {
        ContactValidationResult result = ContactValidator.Validate(
            new ContactRequestDto { Name = " ", Email = "nope", Message = "short" }
        );

        Assert.False(result.IsValid);
        Assert.Equal(3, result.FieldErrors.Count);
        Assert.True(result.FieldErrors.ContainsKey("name"));
        Assert.True(result.FieldErrors.ContainsKey("email"));
        Assert.True(result.FieldErrors.ContainsKey("message"));
    }

    [Fact]
    public void A_turnstile_token_is_trimmed_and_not_required()
    {
        ContactValidationResult present = ContactValidator.Validate(Valid(turnstile: "  ts  "));
        Assert.Equal("ts", present.Contact!.TurnstileToken);

        ContactValidationResult absent = ContactValidator.Validate(Valid(turnstile: "  "));
        Assert.Null(absent.Contact!.TurnstileToken);
    }

    private static ContactValidationResult Validate(
        string name = "Ada Lovelace",
        string email = "ada@example.com",
        string message = "Hello from the form."
    ) => ContactValidator.Validate(Valid(name, email, message));

    private static ContactRequestDto Valid(
        string name = "Ada Lovelace",
        string email = "ada@example.com",
        string message = "Hello from the form.",
        string? turnstile = null
    ) => new()
    {
        Name = name,
        Email = email,
        Message = message,
        TurnstileToken = turnstile,
    };
}
