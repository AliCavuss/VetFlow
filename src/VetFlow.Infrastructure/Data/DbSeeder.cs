using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetFlow.Application.Interfaces;
using VetFlow.Domain.Common;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;
using VetFlow.Domain.Services;
using VetFlow.Infrastructure.Identity;

namespace VetFlow.Infrastructure.Data;

/// <summary>
/// Seeds a realistic demo clinic: staff, price list, vaccine protocol, 13 owners with 18 patients,
/// six months of visits / invoices / payments, vaccination history and the calendar for the coming week.
/// All people and animals are fictional.
/// </summary>
public static class DbSeeder
{
    public static readonly (string Email, string Password, string FullName, string Role, int? VetId)[] DemoUsers =
    [
        ("admin@vetflow.local", "Admin123!", "Clinic Manager", Roles.Admin, null),
        ("elif.kaya@vetflow.local", "Vet12345!", "Dr. Elif Kaya", Roles.Veterinarian, 1),
        ("mert.demir@vetflow.local", "Vet12345!", "Dr. Mert Demir", Roles.Veterinarian, 2),
        ("reception@vetflow.local", "Desk1234!", "Ayşe Yıldız", Roles.Receptionist, null)
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var clock = services.GetRequiredService<IClinicClock>();

        foreach (var role in Roles.All)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        if (!await db.Veterinarians.AnyAsync(ct))
        {
            db.Veterinarians.AddRange(
                new Veterinarian { FullName = "Elif Kaya", Specialty = "Internal medicine", CalendarColor = "#2f6f4f" },
                new Veterinarian { FullName = "Mert Demir", Specialty = "Soft tissue surgery", CalendarColor = "#7c4dbe" },
                new Veterinarian { FullName = "Zeynep Arslan", Specialty = "Exotic animals", CalendarColor = "#2563a8" });
            await db.SaveChangesAsync(ct);
        }

        await SeedUsersAsync(services.GetRequiredService<UserManager<ApplicationUser>>(), db, ct);

        if (!await db.Products.AnyAsync(ct))
        {
            SeedCatalogue(db, clock.Now);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Owners.AnyAsync(ct))
        {
            await SeedClinicHistoryAsync(db, clock, ct);
        }
    }

    private static async Task SeedUsersAsync(UserManager<ApplicationUser> users, ApplicationDbContext db, CancellationToken ct)
    {
        var vets = await db.Veterinarians.OrderBy(v => v.Id).ToListAsync(ct);
        foreach (var (email, password, fullName, role, vetIndex) in DemoUsers)
        {
            if (await users.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                VeterinarianId = vetIndex.HasValue && vets.Count >= vetIndex ? vets[vetIndex.Value - 1].Id : null
            };

            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Seeding {email} failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            await users.AddToRoleAsync(user, role);
        }
    }

    private static void SeedCatalogue(ApplicationDbContext db, DateTime now)
    {
        Product P(string sku, string name, ProductCategory category, string unit, decimal price, decimal vat, decimal stock = 0, decimal reorder = 0)
        {
            var product = new Product
            {
                Sku = sku, Name = name, Category = category, Unit = unit, UnitPrice = price, VatRate = vat,
                TracksStock = category != ProductCategory.Service, ReorderLevel = reorder
            };
            if (stock > 0)
            {
                product.AddStock(stock);
                product.Movements.Add(new StockMovement
                {
                    Type = StockMovementType.Purchase, Quantity = stock, Reference = "Opening stock", CreatedAt = now.AddDays(-200), CreatedBy = "admin@vetflow.local"
                });
            }

            db.Products.Add(product);
            return product;
        }

        // Services (VAT 20 %)
        P("SRV-EXAM", "General examination", ProductCategory.Service, "session", 850, 0.20m);
        P("SRV-CTRL", "Follow-up examination", ProductCategory.Service, "session", 450, 0.20m);
        P("SRV-VACC", "Vaccine administration", ProductCategory.Service, "session", 250, 0.20m);
        P("SRV-DENT", "Dental scaling & polishing", ProductCategory.Service, "session", 3500, 0.20m);
        P("SRV-SPAY-C", "Spay surgery – cat", ProductCategory.Service, "procedure", 6500, 0.20m);
        P("SRV-NEUT-D", "Neuter surgery – dog", ProductCategory.Service, "procedure", 7500, 0.20m);
        P("SRV-XRAY", "X-ray (2 views)", ProductCategory.Service, "study", 1600, 0.20m);
        P("SRV-CBC", "Complete blood count", ProductCategory.Service, "test", 950, 0.20m);
        P("SRV-CHIP", "Microchip implantation", ProductCategory.Service, "procedure", 600, 0.20m);

        // Medications (VAT 10 %)
        P("MED-AMOX", "Amoxicillin/clavulanate 250 mg", ProductCategory.Medication, "tablet", 18, 0.10m, 240, 60);
        P("MED-MELOX", "Meloxicam oral suspension 1.5 mg/ml", ProductCategory.Medication, "bottle", 320, 0.10m, 14, 5);
        P("MED-DEWORM", "Dewormer tablet (praziquantel/pyrantel)", ProductCategory.Medication, "tablet", 95, 0.10m, 120, 40);
        P("MED-FLEA", "Flea & tick spot-on (dog 10–20 kg)", ProductCategory.Medication, "pipette", 420, 0.10m, 8, 10);
        P("MED-CEFTR", "Ceftriaxone 1 g injection", ProductCategory.Medication, "vial", 140, 0.10m, 25, 10);

        // Vaccines (VAT 10 %)
        var rabies = P("VAC-RAB", "Rabies vaccine", ProductCategory.Vaccine, "dose", 380, 0.10m, 40, 15);
        var dhppi = P("VAC-DHPPI", "DHPPi+L combination vaccine (dog)", ProductCategory.Vaccine, "dose", 520, 0.10m, 22, 10);
        var fvrcp = P("VAC-FVRCP", "FVRCP combination vaccine (cat)", ProductCategory.Vaccine, "dose", 480, 0.10m, 6, 10);
        var felv = P("VAC-FELV", "FeLV vaccine (cat)", ProductCategory.Vaccine, "dose", 540, 0.10m, 12, 6);
        var kennel = P("VAC-KC", "Kennel cough vaccine (dog)", ProductCategory.Vaccine, "dose", 410, 0.10m, 18, 6);

        // Supplies & food (VAT 20 %)
        P("SUP-CONE", "Recovery cone (M)", ProductCategory.Supply, "pcs", 180, 0.20m, 3, 5);
        P("SUP-BAND", "Cohesive bandage 7.5 cm", ProductCategory.Supply, "roll", 65, 0.20m, 50, 20);
        P("FOOD-RENAL", "Renal support diet – cat 2 kg", ProductCategory.Food, "bag", 1450, 0.20m, 9, 4);
        P("FOOD-GI", "Gastrointestinal diet – dog 4 kg", ProductCategory.Food, "bag", 1890, 0.20m, 5, 4);

        db.VaccineTypes.AddRange(
            new VaccineType { Name = "Rabies", Species = null, IntervalDays = 365, Product = rabies },
            new VaccineType { Name = "DHPPi+L (distemper combo)", Species = Species.Dog, IntervalDays = 365, Product = dhppi },
            new VaccineType { Name = "Kennel cough", Species = Species.Dog, IntervalDays = 365, Product = kennel },
            new VaccineType { Name = "FVRCP (cat flu combo)", Species = Species.Cat, IntervalDays = 365, Product = fvrcp },
            new VaccineType { Name = "FeLV (feline leukaemia)", Species = Species.Cat, IntervalDays = 365, Product = felv });
    }

    private static async Task SeedClinicHistoryAsync(ApplicationDbContext db, IClinicClock clock, CancellationToken ct)
    {
        var now = clock.Now;
        var today = clock.Today;
        var random = new Random(2026);

        // ---------- Owners & patients ----------
        Owner O(string first, string last, string phone, string? email, bool consent, int daysAgo) => new()
        {
            FirstName = first, LastName = last, Phone = TurkishPhone.Normalize(phone), Email = email,
            ReminderConsent = consent, CreatedAt = now.AddDays(-daysAgo), Address = "İstanbul"
        };

        Patient Pt(Owner owner, string name, Species species, string breed, Sex sex, bool neutered, DateTime born,
            string? chip = null, string? allergies = null, decimal? weight = null, string? color = null) => new()
        {
            Owner = owner, Name = name, Species = species, Breed = breed, Sex = sex, IsNeutered = neutered,
            DateOfBirth = born, MicrochipNumber = chip, Allergies = allergies, LastWeightKg = weight, Color = color,
            CreatedAt = owner.CreatedAt
        };

        var ahmet = O("Ahmet", "Yılmaz", "0532 411 27 90", "ahmet.yilmaz@example.com", true, 420);
        var selin = O("Selin", "Çelik", "0533 208 15 44", "selin.celik@example.com", true, 390);
        var burak = O("Burak", "Şahin", "0542 670 33 12", null, true, 350);
        var ece = O("Ece", "Öztürk", "0505 913 48 26", "ece.ozturk@example.com", false, 330);
        var murat = O("Murat", "Aydın", "0216 345 67 89", null, true, 300);
        var deniz = O("Deniz", "Koç", "0535 772 10 03", "deniz.koc@example.com", true, 260);
        var cem = O("Cem", "Kurt", "0544 129 84 57", null, true, 240);
        var gizem = O("Gizem", "Polat", "0530 655 21 38", "gizem.polat@example.com", true, 210);
        var hakan = O("Hakan", "Erdoğan", "0532 980 41 66", null, false, 190);
        var leyla = O("Leyla", "Aksoy", "0507 318 92 74", "leyla.aksoy@example.com", true, 150);
        var emre = O("Emre", "Güneş", "0541 236 70 19", null, true, 120);
        var nazli = O("Nazlı", "Tekin", "0538 447 58 02", "nazli.tekin@example.com", true, 90);
        var tolga = O("Tolga", "Bulut", "0212 284 66 10", null, true, 30);

        var patients = new List<Patient>
        {
            Pt(ahmet, "Karabaş", Species.Dog, "Kangal", Sex.Male, false, new(2019, 5, 12), "792097000123456", weight: 48.5m, color: "Fawn, black mask"),
            Pt(ahmet, "Pamuk", Species.Cat, "Turkish Angora", Sex.Female, true, new(2021, 3, 2), "792097000123457", weight: 3.9m, color: "White"),
            Pt(selin, "Mia", Species.Cat, "British Shorthair", Sex.Female, true, new(2022, 8, 19), "792097000223310", "Penicillin (skin reaction)", 4.6m, "Blue"),
            Pt(burak, "Rocky", Species.Dog, "Golden Retriever", Sex.Male, true, new(2020, 1, 27), "981020004417721", weight: 32.1m, color: "Golden"),
            Pt(ece, "Limon", Species.Bird, "Budgerigar", Sex.Male, false, new(2023, 6, 1), weight: 0.04m, color: "Yellow-green"),
            Pt(ece, "Tarçın", Species.Cat, "Domestic shorthair (tabby)", Sex.Female, true, new(2014, 9, 15), "792097000331208", weight: 3.2m, color: "Brown tabby"),
            Pt(murat, "Zeus", Species.Dog, "German Shepherd", Sex.Male, false, new(2017, 11, 3), "792097000441902", "NSAID sensitivity (vomiting)", 38.0m, "Black & tan"),
            Pt(deniz, "Fıstık", Species.Rabbit, "Holland Lop", Sex.Female, true, new(2022, 4, 10), weight: 1.8m, color: "Orange"),
            Pt(cem, "Duman", Species.Cat, "Van cat", Sex.Male, true, new(2020, 7, 21), "792097000551067", weight: 5.4m, color: "White, odd eyes"),
            Pt(gizem, "Bella", Species.Dog, "Maltese", Sex.Female, true, new(2021, 2, 14), "981020004418803", weight: 3.1m, color: "White"),
            Pt(gizem, "Luna", Species.Dog, "Pomeranian", Sex.Female, false, today.AddMonths(-5), weight: 1.9m, color: "Orange sable"),
            Pt(hakan, "Paşa", Species.Dog, "Labrador Retriever", Sex.Male, true, new(2016, 6, 30), "792097000661345", weight: 34.7m, color: "Chocolate"),
            Pt(leyla, "Şeker", Species.Cat, "Scottish Fold", Sex.Female, false, new(2023, 12, 5), "792097000771588", weight: 3.3m, color: "Cream"),
            Pt(emre, "Leonardo", Species.Reptile, "Red-eared slider", Sex.Male, false, new(2015, 5, 20), weight: 0.9m),
            Pt(nazli, "Boncuk", Species.Cat, "Persian", Sex.Female, true, new(2019, 10, 8), "792097000881774", weight: 4.1m, color: "Silver"),
            Pt(nazli, "Max", Species.Dog, "Beagle", Sex.Male, true, new(2022, 3, 17), "981020004419964", "Chicken protein (food allergy)", 12.8m, "Tricolour"),
            Pt(tolga, "Çakıl", Species.Dog, "Mixed breed", Sex.Male, false, new(2024, 1, 9), weight: 18.2m, color: "Brindle"),
            Pt(tolga, "Minnoş", Species.Cat, "Domestic shorthair", Sex.Female, false, today.AddMonths(-4), weight: 1.6m, color: "Calico")
        };

        db.Owners.AddRange(ahmet, selin, burak, ece, murat, deniz, cem, gizem, hakan, leyla, emre, nazli, tolga);
        db.Patients.AddRange(patients);
        await db.SaveChangesAsync(ct);

        var vets = await db.Veterinarians.OrderBy(v => v.Id).ToListAsync(ct);
        var products = await db.Products.ToDictionaryAsync(p => p.Sku, ct);
        var vaccineTypes = await db.VaccineTypes.ToListAsync(ct);

        // ---------- Vaccination history ----------
        foreach (var patient in patients.Where(p => p.Species is Species.Dog or Species.Cat))
        {
            foreach (var type in vaccineTypes.Where(t => t.Species is null || t.Species == patient.Species))
            {
                var lastGiven = today.AddDays(-random.Next(15, 430));
                if (patient.DateOfBirth.HasValue && lastGiven < patient.DateOfBirth.Value.AddDays(60))
                {
                    lastGiven = patient.DateOfBirth.Value.AddDays(70);
                    if (lastGiven > today) continue;
                }

                var vet = vets[random.Next(vets.Count)];
                if (lastGiven.AddDays(-365) > (patient.DateOfBirth ?? DateTime.MinValue).AddDays(60))
                {
                    db.Vaccinations.Add(Vaccination.Record(patient, type, lastGiven.AddDays(-365 - random.Next(0, 20)), "elif.kaya@vetflow.local", $"B{random.Next(10000, 99999)}"));
                }

                db.Vaccinations.Add(Vaccination.Record(patient, type, lastGiven, vet.FullName == "Mert Demir" ? "mert.demir@vetflow.local" : "elif.kaya@vetflow.local", $"B{random.Next(10000, 99999)}"));
            }
        }

        // ---------- Six months of completed visits with invoices and payments ----------
        var templates = new (AppointmentType Type, string Reason, string Assessment, string Plan, string[] Skus)[]
        {
            (AppointmentType.Checkup, "Annual health check", "Healthy. Body condition score 5/9.", "Continue current diet, next check in 12 months.", ["SRV-EXAM"]),
            (AppointmentType.Vaccination, "Booster vaccination", "Healthy, fit for vaccination.", "Booster due in 12 months.", ["SRV-VACC"]),
            (AppointmentType.Checkup, "Vomiting since yesterday", "Acute gastritis, mild dehydration.", "Bland diet for 3 days, recheck if vomiting persists.", ["SRV-EXAM", "FOOD-GI"]),
            (AppointmentType.Checkup, "Limping on right hind leg", "Soft tissue strain, right stifle. No fracture on X-ray.", "Meloxicam 5 days, restricted exercise 2 weeks.", ["SRV-EXAM", "SRV-XRAY", "MED-MELOX"]),
            (AppointmentType.Checkup, "Scratching ears", "Otitis externa (bilateral).", "Ear cleaning twice daily, recheck in 10 days.", ["SRV-EXAM", "MED-AMOX"]),
            (AppointmentType.Dental, "Bad breath, tartar", "Grade 2 periodontal disease.", "Dental scaling performed. Brush teeth 3×/week.", ["SRV-DENT", "MED-AMOX"]),
            (AppointmentType.FollowUp, "Follow-up after treatment", "Improving, wound healing well.", "Finish medication, no further visit needed.", ["SRV-CTRL"]),
            (AppointmentType.Checkup, "Fleas noticed by owner", "Flea infestation, mild flea allergy dermatitis.", "Spot-on monthly, treat environment.", ["SRV-EXAM", "MED-FLEA", "MED-DEWORM"]),
            (AppointmentType.Surgery, "Elective neutering", "Healthy – surgery uneventful.", "Cone for 10 days, suture removal in 10–12 days.", ["SRV-CBC", "SRV-NEUT-D", "SUP-CONE", "MED-MELOX"]),
            (AppointmentType.Checkup, "Weight loss, drinking more", "Suspected chronic kidney disease (IRIS stage 2).", "Renal diet, blood test again in 4 weeks.", ["SRV-EXAM", "SRV-CBC", "FOOD-RENAL"])
        };

        var invoicesByYear = new Dictionary<int, int>();
        string NextInvoiceNumber(DateTime date)
        {
            invoicesByYear[date.Year] = invoicesByYear.GetValueOrDefault(date.Year) + 1;
            return $"VF-{date.Year}-{invoicesByYear[date.Year]:D5}";
        }

        void AddCompletedVisit(Patient patient, Veterinarian vet, DateTime start,
            (AppointmentType Type, string Reason, string Assessment, string Plan, string[] Skus) t, int duration, double ageDays)
        {
            var visit = new Visit
            {
                Patient = patient, VeterinarianId = vet.Id, StartedAt = start,
                Subjective = t.Reason,
                Objective = $"T {38.0 + random.Next(0, 12) / 10.0:0.0} °C, HR {random.Next(80, 140)} bpm, mucous membranes pink, CRT < 2 s.",
                Assessment = t.Assessment, Plan = t.Plan,
                WeightKg = patient.LastWeightKg.HasValue ? Math.Round(patient.LastWeightKg.Value * (0.95m + (decimal)random.NextDouble() * 0.1m), 2) : null,
                TemperatureC = 38.0m + random.Next(0, 12) / 10m,
                HeartRate = random.Next(80, 140)
            };
            foreach (var sku in t.Skus)
            {
                var qty = products[sku].Unit == "tablet" ? 10 : 1;
                visit.Items.Add(new VisitItem
                {
                    ProductId = products[sku].Id, Description = products[sku].Name, Quantity = qty,
                    UnitPrice = products[sku].UnitPrice, VatRate = products[sku].VatRate
                });
            }

            if (t.Type == AppointmentType.Vaccination && patient.Species is Species.Dog or Species.Cat)
            {
                var vt = vaccineTypes.FirstOrDefault(v => v.Species == patient.Species) ?? vaccineTypes[0];
                if (vt.Species is null || vt.Species == patient.Species)
                {
                    var vaccineProduct = products.Values.First(p => p.Id == vt.ProductId);
                    visit.Items.Add(new VisitItem
                    {
                        ProductId = vaccineProduct.Id, Description = vaccineProduct.Name, Quantity = 1,
                        UnitPrice = vaccineProduct.UnitPrice, VatRate = vaccineProduct.VatRate
                    });
                }
            }

            visit.Close(start.AddMinutes(duration));
            db.Visits.Add(visit);

            var invoice = new Invoice { Owner = patient.Owner, Visit = visit, CreatedAt = start.AddMinutes(duration) };
            foreach (var item in visit.Items)
            {
                invoice.AddLine(item.Description, item.Quantity, item.UnitPrice, item.VatRate, item.ProductId);
            }

            if (random.NextDouble() < 0.12) invoice.ApplyDiscount(0.10m);
            invoice.Issue(NextInvoiceNumber(start), start.AddMinutes(duration));

            // Older invoices are paid; some recent ones are open, partially paid or overdue.
            var age = ageDays;
            var payRoll = random.NextDouble();
            if (age > 30 || payRoll < 0.7)
            {
                invoice.RegisterPayment(invoice.Balance, random.NextDouble() < 0.65 ? PaymentMethod.CreditCard : PaymentMethod.Cash,
                    start.AddMinutes(duration + 5), "reception@vetflow.local");
            }
            else if (payRoll < 0.85)
            {
                invoice.RegisterPayment(Math.Round(invoice.Total / 2, 2), PaymentMethod.Cash, start.AddMinutes(duration + 5), "reception@vetflow.local");
            }

            db.Invoices.Add(invoice);
        }

        for (var day = today.AddDays(-180); day < today; day = day.AddDays(1))
        {
            if (!ClinicSchedule.Default.IsOpenOn(day)) continue;

            var visitsToday = random.Next(1, 4);
            var vetCursor = new Dictionary<int, DateTime>();
            for (var n = 0; n < visitsToday; n++)
            {
                var patient = patients[random.Next(patients.Count)];
                if (patient.CreatedAt.Date > day) continue;

                var t = templates[random.Next(templates.Length)];
                if (t.Type == AppointmentType.Surgery && patient.Species != Species.Dog) t = templates[0];
                if (t.Skus.Contains("FOOD-RENAL") && patient.Species != Species.Cat) t = templates[2];
                if (t.Type == AppointmentType.Vaccination && patient.Species is not (Species.Dog or Species.Cat)) t = templates[0];

                var vet = patient.Species is Species.Bird or Species.Rabbit or Species.Reptile ? vets[2] : vets[random.Next(2)];
                var start = vetCursor.TryGetValue(vet.Id, out var c) ? c : day.AddHours(9).AddMinutes(15 * random.Next(0, 8));
                var duration = t.Type.DefaultDurationMinutes();
                if (start.AddMinutes(duration).TimeOfDay > ClinicSchedule.Default.Closes.ToTimeSpan()) continue;
                vetCursor[vet.Id] = start.AddMinutes(duration + 15 * random.Next(1, 6));

                var appointment = new Appointment
                {
                    Patient = patient, VeterinarianId = vet.Id, Type = t.Type, Reason = t.Reason,
                    CreatedAt = start.AddDays(-random.Next(1, 10)), CreatedBy = "reception@vetflow.local"
                };
                appointment.SetTime(start, duration);

                // ~6 % no-shows and ~4 % cancellations keep the statistics realistic.
                var roll = random.NextDouble();
                if (roll < 0.06) { appointment.MarkNoShow(start.AddMinutes(20)); db.Appointments.Add(appointment); continue; }
                if (roll < 0.10) { appointment.Cancel("Owner called to cancel"); db.Appointments.Add(appointment); continue; }

                appointment.CheckIn(start.AddMinutes(-5));
                appointment.Start();
                appointment.Complete();
                db.Appointments.Add(appointment);

                AddCompletedVisit(patient, vet, start, t, duration, (today - day).TotalDays);
            }
        }

        await db.SaveChangesAsync(ct);

        // ---------- Calendar: today and the next 6 opening days ----------
        var upcomingReasons = new (AppointmentType Type, string Reason)[]
        {
            (AppointmentType.Vaccination, "Annual booster"), (AppointmentType.Checkup, "Itchy skin"),
            (AppointmentType.FollowUp, "Suture removal"), (AppointmentType.Checkup, "Not eating well"),
            (AppointmentType.Dental, "Dental check"), (AppointmentType.Checkup, "Senior health check"),
            (AppointmentType.Grooming, "Nail trim & ear cleaning"), (AppointmentType.Surgery, "Neutering"),
            (AppointmentType.FollowUp, "Recheck blood values"), (AppointmentType.Emergency, "Ate chocolate")
        };

        var living = patients.Where(p => !p.IsDeceased).ToList();
        for (var offset = 0; offset < 7; offset++)
        {
            var day = today.AddDays(offset);
            if (!ClinicSchedule.Default.IsOpenOn(day)) continue;

            foreach (var vet in vets)
            {
                var cursor = day.AddHours(9).AddMinutes(15 * random.Next(0, 4));
                var count = vet.Id == vets[2].Id ? random.Next(1, 3) : random.Next(3, 7);
                for (var n = 0; n < count; n++)
                {
                    var (type, reason) = upcomingReasons[random.Next(upcomingReasons.Length)];
                    var candidates = vet.Id == vets[2].Id
                        ? living.Where(p => p.Species is Species.Bird or Species.Rabbit or Species.Reptile or Species.Cat).ToList()
                        : living.Where(p => p.Species is Species.Dog or Species.Cat).ToList();
                    var patient = candidates[random.Next(candidates.Count)];
                    if (type == AppointmentType.Surgery && patient.Species != Species.Dog) type = AppointmentType.Checkup;

                    var duration = type.DefaultDurationMinutes();
                    if (cursor.AddMinutes(duration).TimeOfDay > ClinicSchedule.Default.Closes.ToTimeSpan()) break;

                    var appointment = new Appointment
                    {
                        PatientId = patient.Id, VeterinarianId = vet.Id, Type = type, Reason = reason,
                        CreatedAt = now.AddDays(-random.Next(1, 7)), CreatedBy = "reception@vetflow.local"
                    };
                    appointment.SetTime(cursor, duration);

                    db.Appointments.Add(appointment);

                    // Today: finished appointments get a closed medical record + invoice, the running one an open
                    // visit, and the next ones are waiting in reception.
                    if (offset == 0 && cursor.AddMinutes(duration) <= now)
                    {
                        appointment.CheckIn(cursor.AddMinutes(-5));
                        appointment.Start();
                        appointment.Complete();
                        var template = templates.Any(x => x.Type == type) ? templates.First(x => x.Type == type) : templates[0];
                        AddCompletedVisit(patient, vet, cursor, template with { Reason = reason }, duration, 0);
                    }
                    else if (offset == 0 && cursor <= now)
                    {
                        appointment.CheckIn(cursor.AddMinutes(-5));
                        appointment.Start();
                        db.Visits.Add(new Visit
                        {
                            Patient = patient, VeterinarianId = vet.Id, StartedAt = cursor, Subjective = reason,
                            WeightKg = patient.LastWeightKg, TemperatureC = 38.6m
                        });
                    }
                    else if (offset == 0 && cursor <= now.AddMinutes(45))
                    {
                        appointment.CheckIn(now.AddMinutes(-random.Next(2, 15)));
                    }
                    cursor = cursor.AddMinutes(duration + 15 * random.Next(0, 5));
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
