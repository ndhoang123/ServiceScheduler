namespace ServiceScheduler.Api.Models;

// ascending order so that >= comparisons express "at least this capability"
public enum BayCapabilityTag { General = 0, HeavyRepair = 1, EvCertified = 2 }
