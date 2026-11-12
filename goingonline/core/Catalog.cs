using UnityEngine;

namespace GoingOnline
{
    /// <summary>All prices and rates of the provider in one place (KONZEPT.md has the reasoning and the sources).
    /// Money is per game day unless a name says otherwise.</summary>
    internal static class Cat
    {
        // building kinds
        public const byte KindHome = 0, KindRetail = 1, KindOffice = 2, KindWarehouse = 3, KindVilla = 4;
        // devices at the customer
        public const byte DevNone = 0, Dev30 = 1, Dev31 = 2, DevOnt = 3;
        // who owns the device
        public const byte OwnRent = 0, OwnBought = 1, OwnSelf = 2;
        // ticket types
        public const byte TtInstall = 0, TtDevice = 1, TtWifi = 2, TtHouse = 3, TtAmp = 4, TtIngress = 5, TtPower = 6,
                          TtFiberCut = 7, TtCmts = 8, TtHeadend = 9,
                          TtOrder = 10,   // a new contract the customer service still has to process
                          TtPort = 11,    // waiting for the old provider to hand over the phone number (or for free numbers)
                          TtOn = 12,      // a switching order: connect the tap of a new coax customer
                          TtOff = 13,     // a switching order: disconnect the tap of a coax customer who cancelled
                          TtComplaint = 14, // complaints the customer service answers (one ticket per reason, technician and day)
                          TtLinkCut = 15,   // GO-HUBS-1: an excavator cut the feeder fibre of a hub
                          TtHouseAmp = 16;  // GO-UP-1: a building's house amplifier is swapped for high split

        /// <summary>A fault (of the network, the headend or at a customer's) - not an order, a switching job or an installation.</summary>
        public static bool IsFault(byte type) => (type <= TtCmts && type != TtInstall) || type == TtLinkCut;

        // a customer's own fault behind a fault visit (Ticket.Cause of a device fault; 0 = the provider's own device)
        public const byte CauseOwnModem = 1, CauseTv = 2, CausePhone = 3, CausePower = 4, CauseCable = 5;
        // why customers complain (Ticket.Cause of a complaint)
        public const byte WhyRude = 1, WhyFee = 2, WhyMissed = 3, WhyHotline = 4, WhySlow = 5, WhyMove = 6,
                          WhyBill = 7, WhyTv = 8, WhyPrice = 9, WhyOutage = 10;   // GO-FAULTS-2

        // moves, fees and complaints (user 2026-11-06: "Umzugsgebühr ... Schalter zum Abklemmen alte Adresse und Aufschalten
        // neue Adresse", "Entstörungspauschale wegen Kundenfehlverhalten", "Beschwerde: Techniker unfreundlich")
        public const float MoveRate = 0.0008f, MoveSameHood = 0.5f, MoveFeeRef = 30f, MoveFeeMax = 100f;
        public static float MoveStay(float fee) => Mathf.Clamp(1f - (fee - MoveFeeRef) / 200f, 0.6f, 1f);   // a dear fee: some cancel instead
        public const float CustomerFaults = 0.0012f, RemoteHelp = 0.6f, NoShow = 0.06f;   // GO-FAULTS-2: twice as many
        public const float ServiceFeeRef = 45f, ServiceFeeMax = 150f;
        public const float ComplaintWork = 0.25f, ComplaintChurn = 0.03f, Training = 300f;
        public const int TrainingDays = 30;
        /// <summary>The chance of a complaint about a technician after a visit, by the manner (1 rude .. 5 very friendly).</summary>
        public static float RudeChance(int manner) => manner <= 1 ? 0.05f : manner == 2 ? 0.025f : manner == 3 ? 0.008f : manner == 4 ? 0.004f : 0.002f;

        // staff roles
        public const byte RoleTech = 0, RoleMaster = 1, RoleSwitch = 2, RoleService = 3, RoleSupport = 4, RoleSales = 5;
        public static readonly byte[] Roles = { RoleTech, RoleSwitch, RoleMaster, RoleSales, RoleService, RoleSupport };
        public static bool NeedsVan(byte role) => role == RoleTech || role == RoleSwitch;
        public static bool NeedsCar(byte role) => role == RoleMaster || role == RoleSales || NeedsVan(role);
        public static bool NeedsDesk(byte role) => role == RoleMaster || role == RoleService || role == RoleSupport;
        public static float Wage(byte role, int skill) =>
            role == RoleMaster ? 22f + 4f * skill : role == RoleSwitch ? 12f + 2.5f * skill : role == RoleService ? 11f + 2f * skill
            : role == RoleSupport ? 15f + 3f * skill : role == RoleSales ? 13f + 3f * skill : 14f + 3f * skill;

        // permission contracts with the owners (Gestattungsverträge, user 2026-11-06): two game years, then renewed or tendered
        public const int PermitDays = 120, RenewNoticeDays = 10;
        public const float SalesRenew = 0.7f, SalesUnitsPerHour = 60f, SalesBoost = 1.0f;

        // when there are too few people: contractors take jobs waiting for 8 hours (a price per job, a cap per day), overtime
        public const float SubTechJob = 95f, SubSwitchJob = 35f, SubOrderJob = 3f, SubCall = 1.5f, SubWait = 8f;
        public const int SubTechCap = 12, SubSwitchCap = 30, SubServiceCap = 150;
        public const float OvertimeHours = 1.25f, OvertimeWage = 1.375f;   // 10 instead of 8 hours, the 2 more at time and a half

        // workplaces and vehicles: a desk set up at the headend, the player's own vehicles parked there
        public const float Desk = 2500f, DeskDay = 4f, FuelDay = 12f, FleetRadius = 80f;
        public const int VanCargo = 14;   // from this cargo capacity on a vehicle counts as a van (Mersaidi Dash, UMC Desert ...)

        // telephone (VoIP) and IPTV
        public const float Voice = 30000f, NumberBlock = 300f, PortFee = 12f, Interconnect = 0.05f, PhoneWorth = 0.35f,
                           PortShare = 0.7f;
        public const int PortDays = 2;
        public const float Iptv = 60000f, BuyStb = 55f, IptvExtra = 0.15f, IptvChannelMbit = 6f, IptvWatch = 0.55f, IptvReplay = 0.1f;
        public const int IptvChannels = 120, IptvMulticastMax = 80;

        // customer service and technical support
        public const float CallShare = 0.4f, CallWork = 0.05f, FounderService = 0.5f, RemoteFix = 0.4f, DiagWork = 0.25f,
                           DiagFactor = 0.8f, MasterFaults = 0.6f, MasterWork = 0.7f;

        // the start (user 2026-11-06: "so anpassen, dass man mit Startkapital anfangen kann"; the founder loan went out again
        // the same day: "warum Kredit - raus damit"): a smaller CMTS to begin with (above), vans and cars to lease when
        // none are parked at the headend. Money to start with comes from the player's own business or the game's banks.
        public const float VanLease = 45f, CarLease = 30f;

        // size pays (user 2026-11-06: "am Anfang wenig Gewinn, je mehr Kunden mehr Gewinn"): a volume discount on devices,
        // set-top boxes, smartcards, Pay-TV wholesale and interconnection - from 500 customers on, up to 20 % at 5,000
        public static float Volume(int customers) => Mathf.Clamp01((customers - 500f) / 4500f) * 0.2f;

        // the age of a connection (user 2026-11-06: "je neuer der Anschluss, desto billiger die Wartung"): maintenance per
        // unit and day grows with every game year, and so do the faults; fiber ages slower
        public const float MaintCoax = 0.01f, MaintCoaxYear = 0.02f, MaintFiber = 0.005f, MaintFiberYear = 0.005f;
        public const float AgeFaultsCoax = 0.4f, AgeFaultsFiber = 0.1f, Refurbish = 0.6f;   // refurbishing: 60 % of the permission and transfer point

        // repair material (user 2026-11-06: "bei Kabelschäden alt ist billig, neu ist teuer"): the old coax is patched for
        // little, a fiber cut needs a splice crew
        public static float RepairMaterial(byte type) => type switch
        {
            TtHouse => 60f, TtAmp => 150f, TtIngress => 30f, TtPower => 120f, TtFiberCut => 1800f, TtCmts => 400f, TtLinkCut => 2500f, TtHouseAmp => HouseAmp, _ => 0f,
        };

        public const float FirstNodeHome = 6000f;

        // the connection fee, once per new connection (as real providers charge it): it pays the devices at the start; at
        // 40 $ customers do not mind, more puts people off, 0 $ draws a few more
        public const float FeeRef = 40f, FeeMax = 150f;
        public static float FeeDraw(float fee) => Mathf.Clamp(1f - (fee - FeeRef) / 300f, 0.55f, 1.15f);   // the first node in the headend's own neighbourhood: the fiber is short

        // the competitor (Rival.cs): its campaign makes it 8 % cheaper, 20 % fewer sign and 0.3 % more leave a day; on a day
        // its network fails 60 % more sign; it cuts its prices by at most 20 %
        public const float RivalMinPrice = 0.8f, RivalPromoPrice = 0.92f, RivalPromoJoin = 0.8f, RivalPromoChurn = 0.003f,
                           RivalOutageJoin = 1.6f, RivalOutage = 0.004f;

        // the headend
        public const float Cmts30 = 25000f, Cmts31 = 80000f, Olt = 150000f, Sat = 25000f, Transmod = 800f, Cas = 40000f,
                           MoveHeadend = 10000f;

        // coax nodes
        public const int HomesPerNode = 500;
        public const float FirstNode = 12000f, MoreNode = 9000f, Node31 = 8000f;
        public const int NodeHours = 24, Upgrade31Hours = 12;

        // spectrum per node: 96 slots of 8 MHz; TV transponders take one each
        public const int Slots = 96, D30Slots = 32, OfdmSlots = 24, MaxTransponders = 60, ChannelsPerTransponder = 8;
        // GO-CAP-1 (user 2026-11-08: "je mehr CMTS, desto mehr Geräte möglich ... ein Gerät kann nicht beliebig freischalten"):
        // how many customers one device in the headend serves; for more, set up another one
        public const int CapCmts30 = 500, CapCmts31 = 1000, CapOlt = 1000, CapVoice = 500, CapIptv = 300, CapCas = 500;
        // GO-PORTS-1 (user 2026-11-08: "ja" - every CMTS serves only so many nodes): ports per CMTS, one node on each
        public const int CmtsPorts = 8;

        // 0.8 (user 2026-11-08): offices and customer centers per neighbourhood (Places), the hubs' feeder fibre and ring
        public const int V08Grace = 10;                 // days a provider from before 0.8 keeps the old rules
        public const float UncoveredChurn = 0.006f;     // a day, in a neighbourhood without office or customer center
        public const float LinkBase = 5000f, LinkPerM = 35f, RingBase = 5000f, RingPerM = 35f, RingDetour = 1.3f;
        public const int LinkHours = 48, RingHours = 48;
        public const float RateLinkCut = 0.0015f;       // per feeder and day (an excavator)
        // GO-STAFF-1 (user 2026-11-08: "festlegen, wie viel ein Personal ... bedient und wie viel max. ein Meister an Personal
        // unter sich hat"): customers per person - together about 6-7 people per 1,000 customers, as in the industry
        // (Benton 2022: about 7 employees per 1,000 customers); a network master leads up to 10 people in the field
        public const int PerTech = 400, PerSwitch = 800, PerService = 500, FounderServes = 200, SubServes = 300, MasterSpan = 10;
        public const float Unled = 0.75f;

        // GO-PNM-1 (user 2026-11-08, from the research: proactive network maintenance - the modems' and the CMTS's own measurements
        // find plant faults before customers call; vendors name 10-40 % fewer truck rolls): a licence per day and modem, it
        // works while a network master is on duty and a control room workplace stands in an office
        public const float PnmBase = 40f, PnmPerModem = 0.01f, PnmCalls = 0.5f, PnmFaults = 0.75f, PnmRemote = 0.2f;
        // GO-POWER-1 (user 2026-11-08, from the research: amplifiers and nodes are fed over the coax from power supplies with
        // batteries; headends have a UPS and a diesel generator - e.g. Yolo County asks for 2 hours of standby power): power cuts
        // in a neighbourhood, 1-5 hours; half of its homes are dark themselves
        public const float RatePowerCut = 0.012f, DarkShare = 0.5f, BatteryPerNode = 600f, BatteryDay = 0.5f, GeneratorFuel = 15f, AfterCutFaults = 0.05f;
        public const int BatteryHours = 2, UpsHours = 1, CutMin = 1, CutMax = 5;
        public const float Ups = 12000f, Generator = 35000f;

        // GO-UP-1 (user 2026-11-08 chose it from the research: in Europe the cable's return path is 5-65 MHz; Vodafone's high split
        // to 204 MHz brings up to 1 Gbit/s upload per cluster but needs new amplifiers and couplers, and old house amplifiers
        // with a return path only to 65 MHz are swapped): upstream capacity per node, the uploads a coax tariff can have
        public const float UpLow30 = 120f, UpLow31 = 200f, UpHigh = 900f;
        public const int UpLowMax = 50;
        public static readonly int[] CoaxUploads = { 0, 10, 25, 50, 100, 200 };
        public const float HighSplitNode = 7000f, HouseAmp = 80f;
        public const int HighSplitHours = 48;
        // GO-DEEP-1: fiber deep / node+0 - nodes for 50-128 homes without amplifiers (NCTA/SCTE 2017): fewer faults, more room
        public const int DeepPerNode = 128, DeepHours = 72;
        public const float DeepNode = 5500f;
        // GO-RPHY-1: remote PHY - the PHY sits in the node behind digital fibre, a vCMTS server in the office runs the rest
        // (Broadband Library): 64 such nodes and 2,000 modems per server, about 15 % more out of each channel
        public const float Vcmts = 120000f, RphyNode = 3000f, RphyGain = 1.15f;
        public const int VcmtsPorts = 64, CapVcmts = 2000, RphyHours = 24;

        // GO-CACHE-1 (research: streaming services put caches into providers' networks free of charge - filled at night, they
        // serve the evening from inside; Analysys Mason 2022): the server costs its installation, works from 1,000 internet
        // customers; each takes a fifth of the evening traffic off the transit (two at most)
        public const float Cache = 500f, CacheShare = 0.2f, CacheFill = 400f;
        public const int CacheMin = 1000, CacheMax = 2;
        // GO-WHOLESALE-1 (research: O2 sells internet over Vodafone's cable network since 2019): a provider without a network of
        // its own rents lines - from 2,000 customers on; its lines grow towards 8 % of the homes passed where the network is
        // looked after; they load the nodes and the modems' places, pay per line and day, and an outage costs a penalty
        public const string WsName = "Funkwelle Mobil";
        public const int WsMinCustomers = 2000;
        public const float WsPriceRef = 1.10f, WsShare = 0.08f, WsGrow = 0.03f, WsCannibal = 0.9f, WsPenalty = 0.3f;
        // GO-SLA-1: business tariffs with a repair time - worth 1.6 times to firms, their faults go first, over 8 hours off costs
        // half a day's price per customer
        public const int SlaHours = 8;
        public const float SlaWorth = 1.6f, SlaPenalty = 0.5f, SlaPick = 12f;
        // GO-BULK-1 (research: bulk TV contracts with landlords - in Germany until mid-2024 over the service charges): TV for every
        // home of a residential building with 30 homes or more, paid by the owner, two game years
        public const int BulkMinHomes = 30;
        public const float BulkPriceRef = 0.35f, BulkJoin = 1.3f;
        public const float SlotMbit = 40f, OfdmMbit = 1890f;

        // fiber
        public const float PonMbit = 10000f, PonPort = 2500f, FiberSiteBase = 5000f, FiberPerUnit = 110f;

        // connecting a building to the coax network (permission + house transfer point; the house network is there)
        public const float SiteBase = 250f, SitePerUnit = 8f;

        // devices bought for the stock, and shipping a self-install kit
        public const float BuyM30 = 45f, BuyM31 = 95f, BuyOnt = 70f, BuyCard = 12f, Ship = 6f;

        // the market
        public const float MaxShare = 0.85f, JoinRate = 0.08f, BaseChurn = 0.004f, OwnDeviceShare = 0.15f,
                           TvOnlyShare = 0.20f, SelfInstallShare = 0.85f;
        public const float RefTv = 0.60f, RefRent = 0.18f;
        public static float RefPrice(int mbit) => mbit <= 0 ? 0f : 1.10f + 0.55f * Mathf.Log10(Mathf.Max(1, mbit));
        public static float Wealth(bool biz, byte kind) => kind == KindVilla ? 2f : biz ? 1.6f : 1f;

        // traffic: the busy-hour average per customer (Mbit/s) and the share of it per hour of the day
        public static float Tavg(int mbit) => mbit <= 0 ? 0f : 2.0f + 0.006f * mbit;
        /// <summary>Traffic grows by a quarter every game year since the founding (at most threefold): the network needs upgrades.</summary>
        public static float Growth(float years) => Mathf.Clamp(1f + 0.25f * years, 1f, 3f);
        public static readonly float[] HomeDay =
            { 0.30f, 0.22f, 0.17f, 0.15f, 0.15f, 0.18f, 0.25f, 0.35f, 0.42f, 0.45f, 0.48f, 0.50f,
              0.52f, 0.52f, 0.53f, 0.55f, 0.60f, 0.68f, 0.78f, 0.88f, 0.97f, 1.00f, 0.95f, 0.70f };
        public static readonly float[] BizDay =
            { 0.05f, 0.04f, 0.04f, 0.04f, 0.05f, 0.08f, 0.20f, 0.45f, 0.75f, 0.92f, 1.00f, 1.00f,
              0.95f, 0.98f, 1.00f, 0.97f, 0.85f, 0.60f, 0.35f, 0.20f, 0.12f, 0.10f, 0.08f, 0.06f };

        // IP transit: Mbit/s and $ per Mbit/s and month (30 days)
        public static readonly int[] TransitMbit = { 100, 250, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000 };
        public static readonly float[] TransitRate = { 6f, 4.5f, 3.5f, 2.5f, 2f, 1.5f, 1.0f, 0.8f, 0.6f, 0.4f };
        public static float TransitDay(int tier) => TransitMbit[tier] * TransitRate[tier] / 30f;
        public const float Overage = 1.5f, PeeringPort = 2000f, PeeringDay = 40f, PeeringShare = 0.35f, PeeringMbit = 10000f;

        // technicians
        public const float VanDay = 20f, NightBonus = 1.25f;
        public static float Wage(int skill) => Wage(RoleTech, skill);
        /// <summary>Tickets done customer by customer (an hour of work per installation, minutes per order or switching job).</summary>
        public static bool PerUnit(byte type) => type == TtInstall || type == TtOrder || type == TtOn || type == TtOff || type == TtComplaint;
        public static float SkillTime(int skill) => 1.4f - 0.1f * skill;       // 5 stars: 0.9 of the time, 1 star: 1.3

        // unlocks (customers)
        public const int Unlock31 = 300, UnlockFiber = 1500;

        // compensation after § 58 TKG: from the 3rd day, per customer and day
        public static float Comp(float dayPrice, int outageDay) =>
            outageDay < 3 ? 0f : outageDay < 5 ? Mathf.Max(5f, dayPrice * 30f * 0.10f) : Mathf.Max(10f, dayPrice * 30f * 0.20f);
        public static float MissedAppointment(float dayPrice) => Mathf.Max(10f, dayPrice * 30f * 0.20f);

        // faults: per day, the base the rate is multiplied with (see Sim.Faults). GO-FAULTS-2 (user 2026-11-08: "zu wenig
        // Störungen, zu wenig Beschwerden"): twice the rates of 0.7.0
        public const float RateDevice = 0.001f, RateOnt = 0.0004f, RateWifi = 0.0016f, RateHouse = 0.003f,
                           RateAmp = 0.008f, RateIngress = 0.012f, RatePower = 0.002f, RateFiberCut = 0.0016f,
                           RateCmts = 0.0006f;

        /// <summary>Hours of work, technicians needed, and whether customers lose service (else they are slowed down).</summary>
        public static (float work, int need, bool outage) Fault(byte type)
        {
            switch (type)
            {
                case TtInstall: return (1.0f, 1, false);
                case TtDevice: return (0.5f, 1, true);
                case TtWifi: return (0.3f, 1, false);
                case TtHouse: return (1.5f, 1, true);
                case TtAmp: return (2.0f, 1, true);
                case TtIngress: return (4.0f, 1, false);
                case TtPower: return (1.5f, 1, true);
                case TtFiberCut: return (6.0f, 2, true);
                case TtCmts: return (2.0f, 1, true);
                case TtLinkCut: return (8.0f, 2, true);   // a splice crew on the trunk
                case TtHouseAmp: return (0.75f, 1, false);   // a house amplifier swapped in the basement
                case TtOrder: return (0.08f, 1, false);   // per customer
                case TtPort: return (0f, 0, false);       // nobody works on it - the old provider's days
                case TtOn: return (0.1f, 1, false);       // per customer - one visit to the basement switches many
                case TtOff: return (0.1f, 1, false);      // per customer
                case TtComplaint: return (ComplaintWork, 1, false);   // per complaint
                default: return (1.0f, 1, true);
            }
        }

        // Pay-TV wholesalers (invented): id, wholesale per subscriber/day, minimum per day, recommended price, demand
        public sealed class PayTvOffer
        {
            public string Id = "", Name = "", Key = "";
            public float Cost, Minimum, Recommended, Demand;
        }

        public static readonly PayTvOffer[] PayTvOffers =
        {
            new PayTvOffer { Id = "sport", Name = "Arena Sport", Key = "go_paytv_sport", Cost = 0.60f, Minimum = 30f, Recommended = 1.10f, Demand = 0.18f },
            new PayTvOffer { Id = "film", Name = "Silberleinwand", Key = "go_paytv_film", Cost = 0.42f, Minimum = 20f, Recommended = 0.80f, Demand = 0.15f },
            new PayTvOffer { Id = "kids", Name = "Kinderplanet", Key = "go_paytv_kids", Cost = 0.15f, Minimum = 0f, Recommended = 0.35f, Demand = 0.10f },
            new PayTvOffer { Id = "premium", Name = "Diamant Premium", Key = "go_paytv_premium", Cost = 1.00f, Minimum = 60f, Recommended = 1.80f, Demand = 0.06f },
        };

        public static PayTvOffer? Offer(string id)
        {
            foreach (var o in PayTvOffers) if (o.Id == id) return o;
            return null;
        }

        // names for applicants
        public static readonly string[] First =
        {
            "Alex", "Sam", "Jordan", "Robin", "Kim", "Charlie", "Mika", "Noah", "Lea", "Jonas", "Emma", "Ben", "Mia", "Paul",
            "Lena", "Finn", "Sara", "Tom", "Nina", "Luca", "Elif", "Can", "Aylin", "Mehmet", "Olga", "Ivan", "Marta", "Diego",
            "Lucia", "Kenji", "Yuki", "Wei", "Mei", "Priya", "Arjun", "Fatima", "Omar", "Grace", "Henry", "Chloe", "Liam",
            "Zoe", "Ethan", "Ava", "Mason", "Isla", "Leo", "Nora", "Elias", "Hanna",
        };
        public static readonly string[] Last =
        {
            "Smith", "Johnson", "Müller", "Schmidt", "Garcia", "Rossi", "Nowak", "Kowalski", "Yilmaz", "Kaya", "Novak",
            "Silva", "Santos", "Kim", "Lee", "Chen", "Wang", "Tanaka", "Patel", "Khan", "O'Brien", "Murphy", "Dubois",
            "Martin", "Jansen", "Berg", "Larsen", "Horvath", "Popescu", "Petrov", "Weber", "Fischer", "Wagner", "Becker",
        };
    }
}
