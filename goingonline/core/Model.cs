using System.Collections.Generic;

namespace GoingOnline
{
    /// <summary>The whole provider of one player, as it is saved (SaveCarrier: JSON in the save game). Field names are
    /// short on purpose - a city's worth of buildings goes into one string. Times are absolute game hours
    /// (day * 24 + hour).</summary>
    public sealed class IspState
    {
        public int V = 1;
        public string Company = "";
        public string HeadendKey = "";
        public long FoundedHour;
        public int Format;                // 6 = 0.6 (the game's own business types, items and jobs: GAME-TYPES-1), 8 = 0.8 (Places)
        public int V08Day = -1;           // GO-HUBS-1: the day 0.8 came to this provider (its rules apply Cat.V08Grace days later)
        public List<string> AppGear = new List<string>();   // equipment bought in the app before 0.6 (it counts as standing in the headend)
        [Newtonsoft.Json.JsonIgnore] public bool NoCmts;    // no CMTS stands in the headend (the real game; Gear.Refresh)
        public long LastHour = -1;
        public int Seed = 12345;

        // the headend's equipment
        public bool Cmts31, Olt, Sat, Cas;
        public bool Voice, Iptv;          // the voice platform (telephone) and the IPTV platform
        public int NumberBlocks;          // blocks of 1,000 phone numbers bought
        public int NumbersUsed;           // new numbers handed out (ported numbers stay the customers')
        public int Desks;                 // workplaces set up at the headend (desk, PC, headset)
        public string CenterKey = "";     // the customer center: a building the player rents or owns (user 2026-11-06)
        public int CenterDesks;           // workplaces set up there
        public float Calls;               // customer calls waiting today (faults, questions)
        public float CallsToday, CallsAnswered;
        public int Transponders;          // TV transponders fed into the cable (one 8 MHz slot each)
        public int Transmods;             // transmodulators bought (two transponders each)
        public bool Split64;              // FTTH splitter 1:64 (else 1:32)
        public bool HeadendDown;          // the headend's building is no longer the player's

        // upstream
        public int TransitTier;           // Catalog.Transit index
        public int PeeringPorts;

        // daily budgets
        public float Upkeep;              // network maintenance
        public float Sales;               // sales and advertising

        public float Reputation = 60f;
        // customer satisfaction (0..100) and its parts of the last day: network, faults, appointments, calls, orders
        public float Satisfaction = 70f, SatNet = 1f, SatFaults = 1f, SatAppoint = 1f, SatCalls = 1f, SatOrders = 1f;
        // what helps when there are too few people: contractors per role, overtime, contracts renewed by themselves
        public bool SubTech, SubSwitch, SubService, Overtime, AutoRenew = true;
        public Dictionary<string, Hood> Hoods = new Dictionary<string, Hood>();
        public Dictionary<string, Site> Sites = new Dictionary<string, Site>();
        public List<Tariff> Tariffs = new List<Tariff>();
        public int NextTariff = 1;
        public Prices Prices = new Prices();
        public Stock Stock = new Stock();
        public List<PayTv> PayTv = new List<PayTv>();
        public List<Tech> Techs = new List<Tech>();
        public List<Candidate> Candidates = new List<Candidate>();
        public int CandidatesDay = -1;
        public List<Ticket> Tickets = new List<Ticket>();
        public int NextId = 1;
        public List<DayBook> Books = new List<DayBook>();
        public DayBook Today = new DayBook();
        public float[] UpLoad = new float[24];   // upstream need per hour of the current day (Mbit/s)
        public List<string> Events = new List<string>();
        public RivalState Rival = new RivalState();
        public int LeaseVans, LeaseCars;              // leased vehicles (they are always at the headend)
        public bool ShowVanPins = true;                                         // GO-VAN-12: the leased vans' points on the city map
        public List<string> BeaconOn = new List<string>();                        // GO-VAN-15: vans whose roof beacon the player switched on (Q+E)
        public Dictionary<string, int> VanNumbers = new Dictionary<string, int>(); // GO-PLATE-1: the own vans' numbers on their plates (vehicle id -> 1, 2 ...)
        public List<string> LeaseIds = new List<string>();                        // GO-VAN-3: the real leased vans (vehicle ids)
        public Dictionary<string, string> VanAway = new Dictionary<string, string>();   // GO-VAN-4: vehicle id -> the customer's building it stands at
        public Dictionary<string, float[]> VanHome = new Dictionary<string, float[]>(); // GO-VAN-4: where it stood at the office (x, y, z, yaw)
        public VanStyle Style = new VanStyle();   // GO-LIVERY-1: the service vans' livery, set in the app
        public float Fee = Cat.FeeRef;                // the one-off connection fee per new connection
        public float MoveFee = Cat.MoveFeeRef;        // the fee for a move to another address
        public float ServiceFee = Cat.ServiceFeeRef;  // the fault service charge when the fault was the customer's (or nobody was home)
        public float SatComplaints = 1f;              // the satisfaction's sixth part: few complaints, answered in time
        public List<string> Flags = new List<string>();
        public int PlayerTicket;          // the ticket the player repairs on site (0 = none)
        public bool Pnm;                  // GO-PNM-1: the network monitoring is switched on (a licence per day)
        public List<PowerCut> Cuts = new List<PowerCut>();   // GO-POWER-1: power cuts going on now
        // GO-WHOLESALE-1: another provider rents lines on the network (accepted, its price per line and day, its lines per neighbourhood)
        public bool Wholesale;
        public float WsPrice = Cat.WsPriceRef;
        public Dictionary<string, int> WsLines = new Dictionary<string, int>();
        public int WsOffered = -1;        // the day the offer came
        // GO-EVENTS-1: events with a decision (storm, big match, excavator, press, test winner, fibre subsidy)
        public List<Happening> Happen = new List<Happening>();
        public Dictionary<string, int> HappenLast = new Dictionary<string, int>();   // kind -> the day it last came
        public int HappenDay = -1;                                                     // the day the last one came (one a day)
        public List<long> BigOuts = new List<long>();                                  // hours of the last outages of 25+ customers
        // GO-WHOLESALE-2: lines between players (Going Public Server)
        public bool OfferLines;                                                        // this network is offered to other players
        public float OfferPrice = Cat.WsPriceRef;                                      // per line and day
        public List<PeerRent> Rents = new List<PeerRent>();                            // lines rented on other players' networks
        public Dictionary<string, PeerTenant> Tenants = new Dictionary<string, PeerTenant>();   // players renting lines here (their id)
    }

    /// <summary>GO-WHOLESALE-2: lines this provider rents on another player's network.</summary>
    public sealed class PeerRent
    {
        public string Owner = "", Company = "";   // the network's owner (player id) and its company
        public float Price;                       // per line and day (the offer's latest)
        public int Since, Missing;                // the first day; days in a row the offer was not there
        public Dictionary<string, int> Lines = new Dictionary<string, int>();   // neighbourhood -> lines
    }

    /// <summary>GO-WHOLESALE-2: another player renting lines on this network (from their latest bill).</summary>
    public sealed class PeerTenant
    {
        public string Company = "";
        public int LastDay = -1;                  // the day of the last bill booked (each day once)
        public float LastAmount;
        public Dictionary<string, int> Lines = new Dictionary<string, int>();
    }

    /// <summary>GO-EVENTS-1: an event (Events.K*); times are absolute game hours.</summary>
    public sealed class Happening
    {
        public int Id;
        public byte Kind;
        public string Hoods = "";         // where: neighbourhood ids, comma-separated
        public long Start, End;           // the storm's or the match's hours; the claim's answer; the subsidy's deadline
        public long Due = -1;             // the decision is open until this hour (-1 = nothing to decide)
        public int Choice = -1;           // -1 open, 0 no, 1 yes
        public float Cost;                // what "yes" costs (the subsidy: the penalty)
        public float Gain;                // the damages that may come back; the subsidy per building
        public int Goal, StartDay;        // the subsidy: buildings promised, counted from this day
        public int Affected;              // the excavator: connections cut off
        public string Name = "";          // the match: final, derby, int
    }

    /// <summary>GO-POWER-1: the city's power is off in a neighbourhood (absolute game hours).</summary>
    public sealed class PowerCut
    {
        public string Hood = "";
        public long Start, End;
        public float Fuel;                // generator hours paid already
    }

    /// <summary>A neighbourhood: its coax nodes and its fiber.</summary>
    public sealed class Hood
    {
        public string Id = "";
        public int Nodes;                 // built coax nodes (each serves up to Catalog.HomesPerNode units)
        public int NodesBuilding;         // nodes under construction
        public int BuildHours;            // hours until they are ready
        public bool D31;                  // DOCSIS 3.1 on this neighbourhood's nodes
        public int D31Hours;              // hours until the upgrade is done
        public bool FiberIn;              // fiber backbone into the neighbourhood (first fiber site)
        public int PonPorts;
        public float[] Util = new float[24];      // coax utilisation per hour of the day (1 = full)
        public float[] FiberUtil = new float[24];
        public float CongHours;           // customer-hours slowed down today
        public float CustHours;           // customer-hours today
        public float CongYesterday;       // share of yesterday's customer-hours slowed down (0..1)
        // GO-HUBS-1 (0.8): the feeder fibre from the headend to this neighbourhood's hub, and a second way (the ring)
        public bool Link, Ring;
        public int LinkHours, RingHours;  // hours until built
        public bool Batteries;            // GO-POWER-1: batteries in the nodes' power supplies (they bridge a power cut)
        // GO-UP-1: the return path - high split (204 MHz) after the nodes are rebuilt and the house amplifiers swapped
        public bool HighSplit, HsHouses;  // running; the nodes are done, the houses' amplifiers not all yet
        public int HsHours;
        public float[] UpUtil = new float[24];   // upstream utilisation per hour of the day (1 = full)
        // GO-DEEP-1: fiber deep (node+0) - small nodes without amplifiers; GO-RPHY-1: remote PHY nodes on a vCMTS
        public bool Deep, Rphy;
        public int DeepHours, RphyHours, DeepNodes;
    }

    /// <summary>A connected building (or one under construction).</summary>
    public sealed class Site
    {
        public string Key = "";
        public string Hood = "";
        public int Homes, Biz;            // households and business units
        public byte Kind;                 // Catalog.Kind*
        public bool Coax, Fiber;          // service is up
        public int CoaxHours = -1;        // hours until the coax connection is ready (-1 = not ordered)
        public int FiberHours = -1;
        public bool CoaxOff;              // coax switched off after the move to fiber
        public List<Cohort> C = new List<Cohort>();
        public int Waiting;               // customers waiting for a device
        public int WaitDays;
        public float OutHours;            // customer-hours without service today
        public float CustHours;           // customer-hours today
        public float OutYesterday;        // share of yesterday's customer-hours without service (0..1)
        public int FreeRiders;            // coax customers who cancelled but are not switched off yet
        public int PermitUntil = -1;      // the day the permission contract with the owner ends (-1 = not set yet)
        public int BuiltDay = -1;         // the coax was built (or refurbished) on this day; -1 = at the founding
        public int FiberDay = -1;         // the fiber was built on this day
        public bool Lost;                 // the owner gave the building to another provider
        public int LostUntil;             // ... until this day
        public float Canvassed;           // units the field sales visited today
        // GO-BULK-1: a bulk TV contract with the owner - TV for every home of the building, paid by the owner
        public int Bulk;                  // homes under it
        public int BulkUntil = -1;        // the day it ends
        public float BulkPrice;           // per home and day
        public int BulkAsked = -1;        // the day it was offered (the owner answers the next day)
        public int BulkNo = -100;         // the day the owner said no (asked again ten days later at the earliest)
    }

    /// <summary>Customers of one kind in one building: same tariff, device and ownership.</summary>
    public sealed class Cohort
    {
        public int T;                     // tariff id
        public byte Dev;                  // Catalog.Dev*
        public byte Own;                  // Catalog.Own*
        public bool Biz;
        public bool Fib;                  // served over fiber
        public int N;
    }

    public sealed class Tariff
    {
        public int Id;
        public string Name = "";
        public bool Fiber;                // a fiber tariff (else coax)
        public int Mbit;                  // 0 = TV only
        public bool Tv;
        public bool Ip;                   // the TV comes as IPTV (a set-top box, over the internet line) instead of DVB-C
        public bool Phone;                // with a telephone line (flat rate)
        public int Up;                    // GO-UP-1: upload Mbit/s of a coax tariff (0 = a tenth of the download, 5 to 50); fiber is symmetric
        public bool Sla;                  // GO-SLA-1: a business tariff with a guaranteed repair time
        public float Price;               // per day
        public int Term;                  // minimum term in days (0, 10, 30)
        public bool Active = true;
    }

    public sealed class Prices
    {
        public float Rent30 = 0.15f, Rent31 = 0.22f, RentOnt = 0.20f, RentStb = 0.15f;   // per day
        public float Buy30 = 99f, Buy31 = 169f, BuyOnt = 149f;
    }

    /// <summary>The competitor, Skyline Cable (Rival.cs).</summary>
    public sealed class RivalState
    {
        public float Reputation = 60f;
        public Dictionary<string, RivalHood> Hoods = new Dictionary<string, RivalHood>();
        public Dictionary<string, int> Locked = new Dictionary<string, int>();    // building -> exclusive contract until day
        public List<string> Log = new List<string>();                             // its moves, newest first
    }

    public sealed class RivalHood
    {
        public byte Tech;                 // 0 DOCSIS 3.0 (to 500 Mbit/s), 1 DOCSIS 3.1 (1000), 2 fiber (2000)
        public float Price = 1f;          // its prices against the market price
        public int PriceUntil = -1;       // a price cut holds until this day, then the prices creep back
        public int PromoUntil = -1;       // a win-back campaign runs until this day
        public int OutageUntil = -1;      // its network failed here on this day
        public int TechDay = -1;          // an upgrade is done on this day (-1 = none under way)
    }

    public sealed class Stock
    {
        public int M30, M31, Ont, Cards, Stb;
    }

    public sealed class PayTv
    {
        public string Id = "";
        public float Price;               // the player's retail price per day
        public int Subs;
    }

    public sealed class Tech
    {
        public int Id;
        public byte Role;                 // Catalog.Role*
        public string Hood = "";          // a network master's neighbourhood
        public string Name = "";
        public int Skill;
        public float Wage;                // per hour
        public byte Shift;                // 0 early, 1 late, 2 night
        public int Ticket;                // the ticket worked on (0 = free)
        public float Travel;              // hours of driving left
        public string At = "";            // where the technician is (a building's key; empty = the headend)
        public int Manner = 3;            // how the person treats customers: 1 rude .. 5 very friendly
        public string Emp = "";           // the game's employee (0.6: hired at a recruitment agency); empty = hired in the app before 0.6
        public int TrainedDay = -100;     // the last customer-care training
        public List<int> ComplaintDays = new List<int>();   // days of complaints about this person (the last 30)
        public string Pin = "";           // GO-FLEET-1: a vehicle of this person's own (Fleet.Car.Id, set in BizMan's fleet page); empty = any free one
        [Newtonsoft.Json.JsonIgnore] public string Base = "";   // GO-STAFF-1: the building the person is assigned to (Crew.Sync)
        [Newtonsoft.Json.JsonIgnore] public string Car = "";    // the vehicle this hour (Fleet.Assign)
        [Newtonsoft.Json.JsonIgnore] public string CarId = "";  // ... and its id
        [Newtonsoft.Json.JsonIgnore] public bool Desk;          // a workplace this hour
        [Newtonsoft.Json.JsonIgnore] public byte Missing;       // 0 ok, 1 no vehicle, 2 no workplace, 3 off duty
        [Newtonsoft.Json.JsonIgnore] public bool Equipped;      // had what the role needs at the last hour on duty
    }

    public sealed class Candidate
    {
        public byte Role;
        public string Name = "";
        public int Skill;
        public float Wage;
        public int Manner = 3;
    }

    public sealed class Ticket
    {
        public int Id;
        public byte Type;                 // Catalog.TT*
        public string Hood = "";
        public string Site = "";          // empty for faults of a node, the fiber or the headend
        public int Affected;              // customers without service (or slowed down)
        public long Opened;               // absolute hour
        public float Work, Done;          // hours
        public int Need = 1;              // technicians needed
        public List<int> Techs = new List<int>();
        public long Due;                  // installations: the appointment (absolute hour)
        public bool Player;               // the player has started repairing it on site
        public int CompDays;              // days compensation was paid for
        // installations: the customers waiting for it
        public int T; public byte Dev; public byte Own; public bool Biz; public bool Fib; public int N;
        public bool Move;                 // a move from coax to fiber
        public bool Diag;                 // diagnosed by the technical support (less work on site)
        public bool NoNumber;             // porting tickets: waiting for free phone numbers, not for the old provider
        public int Ported;                // orders: how many of them bring their phone number
        public byte Cause;                // a device fault: the customer's own fault (Cat.Cause*, 0 = the provider's device); a complaint: why
        public bool Relocation;           // switching for a customer who moves (off at the old address, on at the new one)
        public string About = "";         // a complaint: the technician it is about
        public bool Visited;              // a technician is at the customer's door (nobody-home is checked once a visit)
        public bool Late;                 // a move that took too long has been complained about
    }

    public sealed class DayBook
    {
        public int Day;
        public float Subs, Rent, Sold, PayTvIn;                          // income
        public float Fees;                                               // connection fees (once per new connection)
        public float MoveFees, ServiceFees;                              // move fees, fault service charges
        public int Moves, MovesLost, Visits, OwnFault, NoShows, Complaints, ComplaintsDone;
        public float Transit, PayTvOut, Wages, Comp, Upkeep, Ship, Tax;  // running costs
        public float Phone, Fleet, Contractors;                          // interconnection and porting, vehicles and workplaces, contractors
        public float Lease;                                              // leased vans and cars
        public float Maint, Repair;                                      // maintenance of the connections by their age, repair material
        public float Permits;                                            // renewed permission contracts
        public float Power;                                              // power and maintenance of the headend's equipment (0.6)
        public float Monitor;                                            // GO-PNM-1: the network monitoring's licence
        public float Wholesale, Bulk;                                    // GO-WHOLESALE-1, GO-BULK-1: income
        public float Events, EventsIn;                                   // GO-EVENTS-1: decisions' costs and penalties; damages and subsidies
        public float PeerSubs, PeerRent, PeerIn;                         // GO-WHOLESALE-2: customers on others' networks; their rent; renters' payments
        public float WorkTech, WorkSwitch, WorkService, WorkSupport;     // hours of work that came in today, per role
        public int MissedCust, InstalledCust, SubJobsTech, SubJobsSwitch, SubJobsService;
        public float Build, Gear;                                        // investments: network, devices for the stock
        public int Customers, Joined, Left, Faults, Fixed;
        public float CallsIn, CallsDone;

        [Newtonsoft.Json.JsonIgnore] public float Income => Subs + Rent + Sold + PayTvIn + Fees + MoveFees + ServiceFees + Wholesale + Bulk + EventsIn + PeerSubs + PeerIn;
        [Newtonsoft.Json.JsonIgnore] public float Running => Transit + PayTvOut + Wages + Comp + Upkeep + Ship + Phone + Fleet + Contractors + Lease + Maint + Repair + Permits + Power + Monitor + Events + PeerRent;
        /// <summary>The day's result before the levy and without investments.</summary>
        [Newtonsoft.Json.JsonIgnore] public float Operating => Income - Running;
    }

    /// <summary>GO-LIVERY-1 (user 2026-11-10: Entwurf 1 of the livery into the game, adjustable in the app): how the
    /// provider's service vans look. The offers on them come from the equipment in the headend, the van's colour from the
    /// dealer (leased vans are white).</summary>
    public sealed class VanStyle
    {
        public int Motif;                    // 0 fibre light, 1 technician at work, 2 lettering only, 3 logo only (the old look)
        public int Panel = 0x1E5AA8;         // the colour panel's colour (RGB)
        public bool Logo = true;             // the business logo on the panel
        public bool Tech = true;             // "Techniker im Einsatz" on the front doors
        public bool Cable = true;            // the cable with light on the sliding doors
        public string Slogan = "";           // "" = "#" and the name in capitals
        public string Contact = "";          // "" = the default line
        public int TextPct = 100, LogoPct = 100;   // GO-LIVERY-4: the lettering's and the logo's size (per cent)
    }
}
