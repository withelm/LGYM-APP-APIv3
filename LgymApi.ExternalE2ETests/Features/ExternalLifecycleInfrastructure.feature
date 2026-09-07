@external
@serial
@ExternalInfrastructure
Feature: External lifecycle infrastructure
  The external harness resets only its dedicated environment before each infrastructure case.

  Scenario: EXT-LIFECYCLE-001: reset runs once for the first serialized infrastructure case
    Given external lifecycle preparation completed

  Scenario: EXT-LIFECYCLE-002: reset runs once for the second serialized infrastructure case
    Given external lifecycle preparation completed
