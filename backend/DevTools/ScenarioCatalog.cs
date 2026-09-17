using CourseInquiryDashboard.Models;

namespace CourseInquiryDashboard.DevTools;

/// <summary>A named scenario and the rows it produces (built on demand).</summary>
public sealed record ScenarioDefinition(string Name, string Description, Func<IReadOnlyList<SeedInquiry>> Build);

/// <summary>
/// The fixed catalog of dev seed scenarios. <see cref="Demo"/> is a curated,
/// realistic dataset (all five statuses, two pages, a few null optional fields);
/// the other scenarios are derived from it or generated to target a specific UI
/// condition the dashboard should handle.
/// </summary>
public static class ScenarioCatalog
{
    // NOTE: Demo is declared before All/Infos so it is initialized first — Infos
    // evaluates the scenario builders eagerly and would otherwise read a null Demo.
    private static readonly IReadOnlyList<SeedInquiry> Demo =
    [
        new("Emma", "Thompson", "emma.thompson@gmail.com", "Intensive Mat-Plus (IMP)", Status.New,
            Phone: "+1-416-555-0142", PreferredLocation: "Toronto",
            Message: "Looking to start my certification this fall. Do weekend cohorts exist?"),
        new("Liam", "O'Brien", "liam.obrien@outlook.com", "Intensive Reformer (IR)", Status.New,
            PreferredLocation: "Vancouver",
            Message: "Is prior mat certification required before the reformer intensive?"),
        new("Sofia", "Moretti", "sofia.moretti@icloud.com", "Total Barre Foundation Course", Status.Contacted,
            Phone: "+1-604-555-0198", PreferredLocation: "Vancouver"),
        new("Noah", "Kim", "noah.kim@gmail.com", "ZEN-GA Mat & Reformer", Status.New,
            Phone: "+1-514-555-0176", PreferredLocation: "Montreal",
            Message: "Interested in the mind-body programming for my studio."),
        new("Aisha", "Rahman", "aisha.rahman@yahoo.com", "Injuries & Special Populations", Status.Registered,
            Phone: "+1-212-555-0110", PreferredLocation: "New York",
            Message: "Confirmed payment last week - excited to begin."),
        new("Lucas", "Silva", "lucas.silva@gmail.com", "CORE Athletic Conditioning", Status.Pending,
            Phone: "+55-11-95555-0133", PreferredLocation: "Los Angeles",
            Message: "Waiting on my employer to approve professional development funds."),
        new("Chloe", "Dubois", "chloe.dubois@gmail.com", "Functional Anatomy for Movement & Injuries", Status.New),
        new("Ethan", "Walker", "ethan.walker@gmail.com", "Intensive Cadillac, Chair & Barrels (ICCB)", Status.Closed,
            Phone: "+1-312-555-0164", PreferredLocation: "Chicago",
            Message: "Decided to postpone to next year."),
        new("Mia", "Nguyen", "mia.nguyen@gmail.com", "Halo Trainer Fundamentals", Status.Contacted,
            Phone: "+1-416-555-0187", PreferredLocation: "Toronto",
            Message: "Following up after the info session."),
        new("Oliver", "Schmidt", "oliver.schmidt@web.de", "Intensive Reformer (IR)", Status.New,
            Phone: "+49-30-5555-0121", PreferredLocation: "Berlin",
            Message: "Do you offer the course materials in German?"),
        new("Isabella", "Rossi", "isabella.rossi@gmail.com", "Total Barre Foundation Course", Status.Pending,
            Phone: "+39-06-5555-0155", PreferredLocation: "London",
            Message: "Comparing dates between the London and Toronto host sites."),
        new("James", "Anderson", "james.anderson@gmail.com", "Intensive Mat-Plus (IMP)", Status.Registered,
            Phone: "+61-2-5555-0190", PreferredLocation: "Sydney"),
        new("Amara", "Okafor", "amara.okafor@gmail.com", "Pilates for Golf", Status.New,
            Phone: "+1-647-555-0173", PreferredLocation: "Toronto",
            Message: "Coaching a golf team and want sport-specific programming."),
        new("Daniel", "Lee", "daniel.lee@gmail.com", "ZEN-GA Mat & Reformer", Status.Contacted,
            Phone: "+65-5555-0148", PreferredLocation: "Singapore",
            Message: "Spoke with an advisor; awaiting the syllabus PDF."),
        new("Grace", "Patel", "grace.patel@gmail.com", "Rehab: Reformer", Status.Closed,
            PreferredLocation: "London",
            Message: "No longer able to travel for the intensive."),
        new("Benjamin", "Martin", "benjamin.martin@gmail.com", "CORE Athletic Conditioning", Status.Registered,
            Phone: "+1-323-555-0129", PreferredLocation: "Los Angeles"),
        new("Yuki", "Tanaka", "yuki.tanaka@gmail.com", "Intensive Cadillac, Chair & Barrels (ICCB)", Status.Pending,
            Phone: "+81-3-5555-0102", PreferredLocation: "Tokyo",
            Message: "Need confirmation the course is taught in English."),
        new("Charlotte", "Wilson", "charlotte.wilson@gmail.com", "Prenatal Pilates", Status.Registered,
            Phone: "+1-416-555-0119", PreferredLocation: "Toronto",
            Message: "Registered and booked travel - see you in October."),
        new("Mateo", "Garcia", "mateo.garcia@gmail.com", "Injuries & Special Populations", Status.New,
            Phone: "+34-91-5555-0166", PreferredLocation: "Miami",
            Message: "Physiotherapist looking to add Pilates rehab to my practice."),
        new("Hannah", "Cohen", "hannah.cohen@gmail.com", "Functional Anatomy for Movement & Injuries", Status.Contacted,
            Phone: "+1-617-555-0184", PreferredLocation: "Boston"),
        new("Arjun", "Sharma", "arjun.sharma@gmail.com", "Total Barre Foundation Course", Status.New,
            Phone: "+971-4-5555-0177", PreferredLocation: "Dubai",
            Message: "Opening a boutique studio and need certified instructors."),
        new("Freya", "Johansson", "freya.johansson@gmail.com", "Halo Trainer Fundamentals", Status.Pending,
            Message: "Holding a spot while I finalize my schedule."),
        new("William", "Brown", "william.brown@gmail.com", "Intensive Reformer (IR)", Status.Registered,
            Phone: "+1-403-555-0138", PreferredLocation: "Calgary",
            Message: "Paid in full via the studio group rate."),
        new("Zoe", "Campbell", "zoe.campbell@gmail.com", "Intensive Mat-Plus (IMP)", Status.Closed,
            Phone: "+1-604-555-0151", PreferredLocation: "Vancouver",
            Message: "Chose a different provider - thanks anyway."),
        new("Gabriel", "Santos", "gabriel.santos@gmail.com", "Pilates for Golf", Status.Pending,
            Phone: "+55-21-5555-0193", PreferredLocation: "Miami"),
        new("Olivia", "Taylor", "olivia.taylor@gmail.com", "ZEN-GA Mat & Reformer", Status.Contacted,
            Phone: "+44-20-5555-0107", PreferredLocation: "London",
            Message: "Left a voicemail; prefers email going forward."),
        new("Henry", "Nakamura", "henry.nakamura@gmail.com", "CORE Athletic Conditioning", Status.Closed,
            Phone: "+1-416-555-0160", PreferredLocation: "Toronto",
            Message: "Personal trainer wanting to expand my scope."),
        new("Layla", "Haddad", "layla.haddad@gmail.com", "Injuries & Special Populations", Status.Contacted,
            Phone: "+1-514-555-0145", PreferredLocation: "Montreal",
            Message: "Asked about payment plans; awaiting reply."),
    ];

    /// <summary>Scenarios keyed by name (case-insensitive).</summary>
    public static IReadOnlyDictionary<string, ScenarioDefinition> All { get; } =
        new List<ScenarioDefinition>
        {
            new("demo",
                "28 realistic inquiries across all five statuses, spanning two pages; a few have null phone/location/message.",
                () => Demo),
            new("single-page",
                "6 inquiries (under one page) with mixed statuses — exercises the no-pagination layout.",
                () => [.. Demo.Take(6)]),
            new("all-new",
                "12 untouched inquiries, all New — a fresh, unworked triage inbox.",
                () => [.. Demo.Take(12).Select(seed => seed with { Status = Status.New })]),
            new("pagination",
                "45 generated inquiries spanning three pages to stress paging and sorting.",
                () => GenerateLoad(45)),
            new("empty",
                "No inquiries — the empty-state UI.",
                () => []),
        }
        .ToDictionary(scenario => scenario.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Listing metadata for the catalog (name, description, size).</summary>
    public static IReadOnlyList<ScenarioInfo> Infos { get; } =
        [.. All.Values.Select(scenario => new ScenarioInfo(scenario.Name, scenario.Description, scenario.Build().Count))];

    /// <summary>
    /// Deterministically generates <paramref name="count"/> inquiries by cycling
    /// through name, course, location, and status pools — used by scenarios that
    /// need volume (pagination) rather than curated copy.
    /// </summary>
    private static IReadOnlyList<SeedInquiry> GenerateLoad(int count)
    {
        string[] firstNames = ["Alex", "Sam", "Jordan", "Taylor", "Casey", "Riley", "Morgan", "Jamie", "Quinn", "Avery"];
        string[] lastNames = ["Reyes", "Novak", "Haddad", "Ivanov", "Costa", "Fischer", "Larsen", "Mendez", "Park", "Blake"];
        string[] courses =
        [
            "Intensive Mat-Plus (IMP)", "Intensive Reformer (IR)", "Intensive Cadillac, Chair & Barrels (ICCB)",
            "Total Barre Foundation Course", "ZEN-GA Mat & Reformer", "Halo Trainer Fundamentals",
            "CORE Athletic Conditioning", "Injuries & Special Populations", "Rehab: Reformer", "Pilates for Golf",
        ];
        string[] locations =
        [
            "Toronto", "Vancouver", "Montreal", "New York", "Los Angeles",
            "London", "Sydney", "Singapore", "Tokyo", "Dubai",
        ];
        Status[] statuses = [Status.New, Status.New, Status.Contacted, Status.Pending, Status.Registered, Status.Closed];

        var rows = new List<SeedInquiry>(count);
        for (var i = 0; i < count; i++)
        {
            var first = firstNames[i % firstNames.Length];
            var last = lastNames[(i * 7) % lastNames.Length];
            rows.Add(new SeedInquiry(
                first,
                last,
                $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}{i + 1}@example.com",
                courses[i % courses.Length],
                statuses[i % statuses.Length],
                Phone: i % 3 == 0 ? null : $"+1-416-555-{1000 + i:D4}",
                PreferredLocation: i % 5 == 0 ? null : locations[i % locations.Length],
                Message: i % 2 == 0 ? $"Generated load-test inquiry #{i + 1}." : null));
        }

        return rows;
    }
}
