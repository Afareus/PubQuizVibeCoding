namespace QuizApp.Server.Application.Challenges;

public static class ChallengeTemplate
{
    public static readonly IReadOnlyList<TemplateQuestion> Questions = new List<TemplateQuestion>
    {
        new(1, 1, "Jaké jídlo bych si nejraději dal/a?",
        [
            new("A", "Pizza"),
            new("B", "Sushi"),
            new("C", "Burger"),
            new("D", "Těstoviny"),
        ]),
        new(2, 2, "Kam bych nejraději vyrazil/a na dovolenou?",
        [
            new("A", "Horská chata"),
            new("B", "Exotická pláž"),
            new("C", "Velkoměsto"),
            new("D", "Kempink v přírodě"),
        ]),
        new(3, 3, "Co bych dělal/a s volným milionem korun?",
        [
            new("A", "Cestoval/a bych po světě"),
            new("B", "Investoval/a bych"),
            new("C", "Koupil/a bych nemovitost"),
            new("D", "Utratil/a bych za zážitky a přátele"),
        ]),
        new(4, 4, "Jaký typ filmu mám nejraději?",
        [
            new("A", "Komedie"),
            new("B", "Akční thriller"),
            new("C", "Romantický"),
            new("D", "Dokumentární"),
        ]),
        new(5, 5, "Jakou aktivitu bych si vybral/a na volný večer?",
        [
            new("A", "Deskovky s přáteli"),
            new("B", "Sledování seriálu doma"),
            new("C", "Procházka nebo sport"),
            new("D", "Výlet do restaurace"),
        ]),
        new(6, 6, "Co mě nejspíš nejvíc potěší?",
        [
            new("A", "Překvapení od blízkých"),
            new("B", "Volný den bez povinností"),
            new("C", "Pochvala za práci"),
            new("D", "Nová zajímavá zkušenost"),
        ]),
        new(7, 7, "Jaký nápoj bych si nejčastěji vybral/a?",
        [
            new("A", "Káva"),
            new("B", "Čaj"),
            new("C", "Džus nebo voda"),
            new("D", "Limonáda nebo pivo"),
        ]),
        new(8, 8, "Jak bych nejraději trávil/a víkend?",
        [
            new("A", "Výlet do přírody"),
            new("B", "Lenošení doma"),
            new("C", "Kulturní akce nebo výstava"),
            new("D", "Setkání s rodinou nebo přáteli"),
        ]),
        new(9, 9, "Co mě nejvíc vystihuje?",
        [
            new("A", "Organizovaný/á a pečlivý/á"),
            new("B", "Spontánní a dobrodružný/á"),
            new("C", "Klidný/á a rozvážný/á"),
            new("D", "Společenský/á a zábavný/á"),
        ]),
        new(10, 10, "Jakou superschopnost bych si vybral/a?",
        [
            new("A", "Čtení myšlenek"),
            new("B", "Teleportace"),
            new("C", "Zastavení času"),
            new("D", "Neviditelnost"),
        ]),
    };
}

public sealed record TemplateQuestion(
    int TemplateQuestionId,
    int OrderIndex,
    string Text,
    IReadOnlyList<TemplateOption> Options);

public sealed record TemplateOption(string OptionKey, string Text);
