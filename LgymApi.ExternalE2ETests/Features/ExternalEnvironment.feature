@external
@serial
@ExternalSmoke
Feature: External environment
  The external harness verifies the unauthenticated web-to-API boundary after its dedicated environment is reset.

  Scenario: EXT-ENVIRONMENT-001: unauthenticated web uses the configured API origin
    Given the baseline dump was restored and the external API recovered
    When the browser opens the configured web URL
    Then a deterministic unauthenticated application surface is visible
    And an application request targets the configured API origin
