---
aw-doc: 1
id: sindri-agent-first-tdd
kind: tdd
title: "Sindri, agent-first: workflows, connections, toolkits and memory"
status: draft
generated: write_packet.py
source-sha256: be1d234b7976afc749ed33d0554427e707501f63a1a41c3b2435a78cb007f76e
repos: [jthorare/agentic-workflow]
---

# Sindri, agent-first: workflows, connections, toolkits and memory

Generated from packet.json by write_packet.py; edit the packet, not this file.

## Phases

### Ph1 Drop profile approval, edits apply on save

### Ph2 Open every verb to agent sessions

### Ph3 Remove recipes, RecipeWorkflow and the factory stations

### Ph4 Remove recipe and pipeline pages; item reads come from links

### Ph5 Graph spec, revisions, validator, interpolation and conditions

### Ph6 GraphWorkflow on Temporal, run store, schedule/manual/condition/notify/prompt blocks

### Ph7 run_agent block on the existing launcher and agent runtime

### Ph8 MCP build tools, `sindri wf` CLI and workflow docs

### Ph9 Remaining v1 blocks with loop guards

### Ph10 Connection store, kinds, keychain secrets, presets, OAuth sign-in and refresh

### Ph11 Poll triggers, cursors, shared polling and catch-up on start and wake

### Ph12 Handoff inbox contract, inbox trigger, claim and submit, local endpoint

### Ph13 Memory stores and facts with local embeddings

### Ph14 Resources (database, disk, sandbox, connection) with local provisioning

### Ph15 Aw.Sindri.Toolkit SDK package and the /new-toolkit scaffold

### Ph16 Toolkit runtime, MCP exposure, call log and the call_tool block

### Ph17 Workflows canvas and runs page

### Ph18 Connections, memory, toolkits and resources list pages

## Requirements

- **REQ-1** When an agent session calls any Sindri verb or MCP tool, Sindri shall perform it and record By: session:<id> on its ledger event.
- **REQ-2** When a profile edit validates, Sindri shall apply it immediately and shall not hold it for approval.
- **REQ-3** When a workflow spec is saved, Sindri shall store it as a new immutable revision with its hash.
- **REQ-4** If a save names a baseRevision that is not the latest, Sindri shall refuse it as a conflict and write nothing.
- **REQ-5** When workflow_validate runs, Sindri shall return every finding with its node, field and a fix.
- **REQ-6** If a workflow has a validation finding, Sindri shall refuse to activate it.
- **REQ-7** When a trigger fires, Sindri shall start a run with id <workflow>@<revision>:<occurrence>.
- **REQ-8** If a run with the same id already exists, Sindri shall not start a second run.
- **REQ-9** While a run of a one-per-workflow workflow is in flight, Sindri shall skip and log a new trigger for that workflow.
- **REQ-10** When Sindri starts or the system wakes, Sindri shall run each schedule whose ticks were missed once, for the latest missed tick.
- **REQ-11** When Sindri starts or the system wakes, Sindri shall read every poll cursor forward to now before resuming its cadence.
- **REQ-12** When a run_agent node runs, Sindri shall launch the session in a fresh worktree inside the sandbox with only the connections the node names.
- **REQ-13** If an agent session ends without a report, Sindri shall fail the node with the reason.
- **REQ-14** When a node fails and its onFailure is stop, Sindri shall mark the run failed and every unrun node skipped.
- **REQ-15** When an OAuth access token is within its refresh window, Sindri shall refresh it.
- **REQ-16** If a connection refresh fails, Sindri shall mark the connection needs-sign-in, add a Needs-you row and fail the nodes that use it with that reason.
- **REQ-17** The system shall never write a connection secret to the ledger, logs, MCP results or test output.
- **REQ-18** When a claimed inbox offer arrives, Sindri shall create an item with its payload and start each workflow whose inbox trigger matches.
- **REQ-19** When a memory write is nearly identical to an existing fact with the same tags, Sindri shall refresh that fact instead of adding one.
- **REQ-20** When a workflow is activated or a toolkit started, Sindri shall provision its declared database, disk and sandbox limits.
- **REQ-21** When a workflow is archived or a toolkit deleted, Sindri shall keep its resources until they are purged.
- **REQ-22** When a toolkit builds clean and passes its tests, Sindri shall serve its tools as <toolkit>.<tool> on /sindri/mcp.

## Test plan

| Row | Check | Verifies |
|---|---|---|
| TP-1 | ProfileLiveTests.A_valid_profile_set_is_live_for_the_next_read: `profile set` then `IApprovedProfile.Latest` returns the new value, no approve step. | Ph1 |
| TP-2 | ProfileLiveTests.An_invalid_profile_edit_is_refused_and_writes_nothing: SND-PROFILE-* finding, file bytes unchanged. | Ph1 |
| TP-3 | ProfileLiveTests.IfHash_refuses_a_write_when_the_file_moved: SND-PROFILE-003 still guards concurrent edits. | Ph1 |
| TP-4 | ProfileApproveVerbTests.profile_approve_prints_the_removal_line_and_exits_0: the verb answers "removed: edits apply on save". | Ph1 |
| TP-5 | SetupDeriverTests.The_Profile_step_has_no_approval_states: only Done and Todo remain. | Ph1 |
| TP-6 | DoctorRulesTests.Doctor_has_no_approval_rows: a changed profile is not a warn. | Ph1 |
| TP-7 | ApprovalsDirectoryCleanupTests.First_start_removes_the_old_approved_folder_once: and appends one ledger note. | Ph1 |
| TP-8 | AwSindriBashSuites.sindri_prove_fixture_runs_without_approve: scripts/tests/sindri-ui-fixture.test.sh passes on the new flow. | Ph1 |
| TP-9 | AuthorizationTests.Every_caller_class_may_run_every_non_local_action_class: table has no refusal except `Local`. | Ph2 |
| TP-10 | LaunchedSessionAuthorizationTests.A_launched_session_may_decide_pause_and_run_desk_actions: flips the old refusal test. | Ph2 |
| TP-11 | ProjectWriteTests.A_session_may_edit_the_Projects_page_and_the_event_names_it: `By` = `session:<id>`. | Ph2 |
| TP-12 | IndexConnectVerbTests.An_agent_session_may_connect_a_source_with_a_token_pointer: no terminal refusal. | Ph2 |
| TP-13 | WriteAttributionTests.Every_write_event_records_owner_or_session_as_its_actor: sweeps the command catalog with a session caller. | Ph2 |
| TP-14 | AgentReportAuthorityTests.A_wrong_launch_secret_is_still_SND_AUTH_003: the one gate that stays. | Ph2 |
| TP-15 | ApiGoldenTests.The_API_goldens_pass_when_run_as_a_session: re-runs the 111 Golden/Api cases with a session caller. | Ph2 |
| TP-16 | CallerContextMiddlewareTests.A_launch_secret_still_downgrades_a_claimed_human_to_a_session: attribution kept. | Ph2 |
| TP-17 | Aw.Architecture.Tests.Sindri_has_no_reference_to_the_deleted_recipe_namespaces: no `Aw.Sindri.Core.Workflow.Recipes\|Registry\|Validation` left. | Ph3 |
| TP-18 | LedgerMigrationTests.Migration_0008_drops_the_recipe_tables_and_keeps_events_after_a_backup: ledger events survive, tables gone. | Ph3 |
| TP-19 | RetiredEventsTests.Retired_workflow_and_line_events_still_deserialize_as_history: old ledger rows read without throwing. | Ph3 |
| TP-20 | StartupTests.Serve_cancels_instances_in_flight_once_with_a_retired_note: one `workflow.retired` per open instance, idempotent on restart. | Ph3 |
| TP-21 | CommandCatalogTests.The_command_list_has_no_recipe_scoping_line_or_docs_check_verbs: catalog golden regenerated. | Ph3 |
| TP-22 | AgentReportAuthorityTests.The_launch_secret_check_survives_the_engine_removal: AgentReportAuthority behavior unchanged. | Ph3 |
| TP-23 | LaunchRequestTests.LaunchRequest_builds_without_the_engine_namespace: the moved ids compile in Core.Launch. | Ph3 |
| TP-24 | AwSindriBashSuites.setup_dry_run_and_install_cs_publish_without_recipe_eval: install-cs.sh no longer runs `recipe eval --shipped`. | Ph3 |
| TP-25 | ItemLinksProjectionTests.A_linked_issue_and_PR_show_as_links_on_the_item_row: `item.linked` folds into `view_item_links`. | Ph4 |
| TP-26 | ViewLinearItemLinksTests.PR_chips_come_from_item_links_not_line_facts: same chip text, "(state unknown)" when unreadable. | Ph4 |
| TP-27 | ProjectorTests.Retired_event_kinds_fold_as_timeline_entries_only: a ledger with `workflow.*` and `line.*` rows replays, rows appear on the timeline. | Ph4 |
| TP-28 | ProjectorTests.A_rebuild_over_an_old_ledger_ends_with_no_stall: `sindri views rebuild` reaches the head. | Ph4 |
| TP-29 | ItemStateTests.Items_with_no_engine_rows_read_as_open_or_closed: New, Done, Cancelled only. | Ph4 |
| TP-30 | NeedsYouProjectionTests.Retired_recipe_rows_are_dropped_not_shown_stale: no ghost rows after upgrade. | Ph4 |
| TP-31 | PageIdTests.The_app_has_no_Recipes_page_and_no_Workflows_page_until_PR_17: navigation and palette entries gone. | Ph4 |
| TP-32 | ReviewsPageTests.The_Reviews_page_renders_without_a_babysitter_panel: panel absent, rest unchanged. | Ph4 |
| TP-33 | GraphValidatorTests.A_spec_with_two_triggers_is_refused_naming_both_nodes: finding carries node ids and a fix sentence. | Ph5 |
| TP-34 | GraphValidatorTests.A_reference_to_a_node_that_is_not_upstream_is_refused: also covers a reference to a downstream node. | Ph5 |
| TP-35 | GraphValidatorTests.A_cycle_is_refused_with_the_path: path printed in spec order. | Ph5 |
| TP-36 | GraphValidatorTests.A_draft_may_have_wiring_gaps_but_activation_refuses_every_finding: two modes of one validator. | Ph5 |
| TP-37 | TemplateTests.A_string_that_is_exactly_one_reference_keeps_the_type_and_a_mixed_string_renders_compact_JSON: boolean stays boolean, object becomes JSON. | Ph5 |
| TP-38 | ExpressionTests.Conditions_follow_precedence_and_truthiness: `not a and b or c`, `exists(x)`, null and empty string falsy. | Ph5 |
| TP-39 | CronExpressionTests.LastTick_returns_the_latest_missed_tick_across_a_DST_change_in_the_timezone: America/Los_Angeles spring forward. | Ph5 |
| TP-40 | SqliteWorkflowStoreContractTests.A_save_with_a_stale_base_revision_is_a_conflict: shared contract with the in-memory fake. | Ph5 |
| TP-41 | GraphWorkflowTests.A_linear_spec_runs_each_node_once_in_order: Aw.Sindri.Engine.Tests, Temporal time-skipping server. | Ph6 |
| TP-42 | GraphWorkflowTests.A_failed_node_halts_the_walk_and_marks_the_rest_skipped: and `onFailure: continue` keeps walking. | Ph6 |
| TP-43 | GraphRunStarterTests.A_second_start_with_the_same_run_id_is_refused: duplicate occurrence, one run row. | Ph6 |
| TP-44 | GraphRunStarterTests.One_per_workflow_skips_a_trigger_while_a_run_is_in_flight_and_logs_it: queue mode waits instead. | Ph6 |
| TP-45 | WorkflowScheduleServiceTests.Three_missed_days_fire_one_run_for_the_latest_tick: fake clock, cron `0 7 * * 1-5`. | Ph6 |
| TP-46 | ConditionBlockTests.A_false_condition_takes_only_the_false_edge: the true branch's nodes are `skipped`. | Ph6 |
| TP-47 | PromptBlockTests.The_prompt_block_routes_through_the_workflowPrompt_route_and_validates_its_schema: bad JSON fails the node with the reason. | Ph6 |
| TP-48 | NodeRunStoreTests.Node_output_over_the_cap_is_truncated_and_flagged: `truncated` set, ledger payload scrubbed by Aw.Redaction. | Ph6 |
| TP-49 | RunAgentBlockTests.A_report_that_matches_the_declared_outputs_completes_the_node: outputs plus `branch, commit, sessionId, usage`. | Ph7 |
| TP-50 | RunAgentBlockTests.A_report_missing_a_declared_field_is_refused_and_the_agent_may_retry: schema error names the field. | Ph7 |
| TP-51 | AgentRecoveryTests.Outputs_in_sindri_out_complete_the_node_when_the_session_ended_unreported: ported from the Step-based test. | Ph7 |
| TP-52 | GraphAgentTakeoverTests.A_takeover_pauses_the_node_timeout_and_handback_resumes_the_remainder: Temporal time-skipping. | Ph7 |
| TP-53 | AgentWatchdogTests.A_session_idle_past_its_limit_fails_the_node_with_idle: reason in the node error. | Ph7 |
| TP-54 | RunAgentLaunchTests.The_branch_is_wf_slug_run_and_the_session_name_uses_the_launch_id: no `:` in either. | Ph7 |
| TP-55 | SandboxGroupsTests.A_node_with_a_repo_gets_the_github_group_and_a_named_connection_adds_its_hosts: HostGroups.Resolve. | Ph7 |
| TP-56 | AgentReportAuthorityTests.A_wrong_launch_secret_is_still_SND_AUTH_003_for_a_node_report: unchanged contract. | Ph7 |
| TP-57 | WorkflowToolsTests.tools_list_includes_every_workflow_tool_with_a_schema: McpGolden for tools/list. | Ph8 |
| TP-58 | ApplyOpsTests.A_stale_base_revision_returns_a_conflict_with_the_current_revision: agent re-reads and retries. | Ph8 |
| TP-59 | ApplyOpsTests.A_failing_op_applies_none_of_the_batch: atomic, spec unchanged. | Ph8 |
| TP-60 | WorkflowDocsTests.Every_block_page_has_an_example_spec_that_validates: runs `GraphValidator` over each embedded example. | Ph8 |
| TP-61 | WorkflowDocsTests.A_question_about_polling_finds_the_poll_block_page: ranking sanity on a fixed set of questions. | Ph8 |
| TP-62 | WorkflowLoopGuardTests.A_session_launched_by_a_run_cannot_run_its_own_workflow: refusal names the run. | Ph8 |
| TP-63 | WfCommandTests.wf_list_and_show_print_the_active_revision_and_who_changed_it: CLI text and `--json`. | Ph8 |
| TP-64 | DriftCheckAcceptanceTests.A_sentence_becomes_a_running_workflow_through_MCP_only: create, run no-drift, run drift, activate, tick opens a PR. | Ph8 |
| TP-65 | ForEachBlockTests.Two_hundred_items_run_in_batches_and_collect_in_item_order: output order equals input order. | Ph9 |
| TP-66 | ForEachBlockTests.Five_hundred_items_stay_under_the_history_budget: asserts parent history length under the limit. | Ph9 |
| TP-67 | OnlyNewBlockTests.A_failed_run_does_not_mark_its_items_as_seen: items return on the next run. | Ph9 |
| TP-68 | WaitBlockTests.A_wait_until_a_time_in_a_timezone_resolves_at_that_instant: time-skipping, DST edge. | Ph9 |
| TP-69 | AskOwnerBlockTests.An_unanswered_question_takes_the_default_branch_at_its_timeout: and an answer before it wins. | Ph9 |
| TP-70 | AnswerQuestionTests.An_agent_session_may_answer_its_own_runs_question_and_the_timeline_says_so: attribution `session:<id>`. | Ph9 |
| TP-71 | RunWorkflowBlockTests.Depth_three_is_allowed_depth_four_is_refused_and_self_start_is_refused: SND-GRAPH codes. | Ph9 |
| TP-72 | HttpBlockTests.A_host_off_the_allowlist_a_loopback_address_and_a_cross_host_redirect_are_refused: no network in the test (fake handler). | Ph9 |
| TP-73 | PkceChallengeTests.The_challenge_is_S256_of_the_verifier_and_the_state_is_checked_on_callback: mismatch is SND-CONN. | Ph10 |
| TP-74 | LoopbackSignInTests.The_callback_stores_tokens_in_the_keychain_and_closes_the_listener: fake keychain, fake OAuth server. | Ph10 |
| TP-75 | LoopbackSignInTests.A_denied_consent_or_a_timeout_leaves_no_connection_row: and no secret. | Ph10 |
| TP-76 | ConnectionRefreshTests.A_token_expiring_within_ten_minutes_is_refreshed_and_the_secret_replaced: refresh token rotation kept. | Ph10 |
| TP-77 | ConnectionRefreshTests.A_failed_refresh_marks_needs_sign_in_adds_a_Needs_you_row_and_fails_dependent_nodes: with the reason text. | Ph10 |
| TP-78 | ConnectionToolsTests.connection_get_never_returns_a_secret_or_token_prefix: McpGolden for tools/list and a get. | Ph10 |
| TP-79 | RunAgentConnectionTests.Only_the_connections_a_node_names_reach_the_session: env and PATH inspected in the fake launcher. | Ph10 |
| TP-80 | ConnectionSecretsTests.Secrets_are_scrubbed_from_logs_errors_and_the_ledger: Aw.Redaction over every failure path. | Ph10 |
| TP-81 | PollConnectionsTests.Three_triggers_on_one_connection_and_event_kind_make_one_provider_call: shared poll, per-trigger filters applied locally. | Ph11 |
| TP-82 | PollConnectionsTests.The_cursor_advances_only_after_the_runs_started: a start failure keeps the cursor. | Ph11 |
| TP-83 | PollConnectionsTests.A_429_with_Retry_After_delays_the_next_poll_and_a_second_failure_doubles_it: fake clock. | Ph11 |
| TP-84 | CatchUpTests.A_catch_up_that_overlaps_a_regular_poll_starts_each_event_once: same delivery ids, one run each. | Ph11 |
| TP-85 | CatchUpTests.A_capped_catch_up_records_one_event_and_leaves_the_cursor_where_it_stopped: cap honored. | Ph11 |
| TP-86 | WakeDetectionTests.A_wall_clock_jump_past_three_ticks_signals_a_wake_and_a_normal_delay_does_not: Stopwatch vs fake TimeProvider. | Ph11 |
| TP-87 | WorkflowPollGoldenTests.A_Linear_label_change_fires_the_workflow_within_one_interval: fake Linear, fake clock. | Ph11 |
| TP-88 | GitHubEventPollerTests.PR_merged_and_review_requested_map_to_the_catalog_events_with_stable_delivery_ids: fixture JSON. | Ph11 |
| TP-89 | InboxContractTests.The_fake_inbox_and_the_http_client_agree_on_list_claim_and_submit: contract test base run over both. | Ph12 |
| TP-90 | PollInboxTests.A_lost_claim_race_409_starts_nothing_and_creates_no_item: informational event only. | Ph12 |
| TP-91 | PollInboxTests.An_offer_claimed_but_not_started_is_retried_without_a_second_claim: claim id persisted first. | Ph12 |
| TP-92 | HandoffItemTests.The_offer_payload_is_kept_in_item_handoff_even_when_the_prompt_exceeds_the_body_limit: 4,000-char body rule. | Ph12 |
| TP-93 | SubmitInboxResultTests.A_finished_run_submits_its_outputs_once_and_a_failed_run_submits_the_failure: idempotent on repeat. | Ph12 |
| TP-94 | HandoffEndpointTests.The_local_endpoint_needs_the_per_start_token_and_refuses_non_loopback_callers: 401 and 403 paths. | Ph12 |
| TP-95 | HandoffEndpointTests.A_local_handoff_creates_an_item_and_fires_a_matching_inbox_trigger: and one that does not match fires none. | Ph12 |
| TP-96 | CheckHandoffSchemaTests.The_OpenAPI_file_validates_and_each_example_payload_gets_its_verdict: bash suite scripts/tests/check-handoff-schema.test.sh. | Ph12 |
| TP-97 | MemoryDedupeTests.The_threshold_separates_the_near_pairs_from_the_related_pairs_in_the_fixture_set: tests/fixtures/memory/pairs.json. | Ph13 |
| TP-98 | WriteMemoryFactTests.A_near_duplicate_with_the_same_tags_refreshes_instead_of_adding: `action: refreshed`, one row. | Ph13 |
| TP-99 | WriteMemoryFactTests.The_same_text_with_different_tags_is_a_new_fact: tags are part of identity. | Ph13 |
| TP-100 | SearchMemoryTests.Recall_ranks_by_cosine_and_respects_loose_balanced_and_strict: fake embedder with fixed vectors. | Ph13 |
| TP-101 | SearchMemoryTests.Without_the_model_recall_falls_back_to_keywords_and_says_so: `mode: keyword`. | Ph13 |
| TP-102 | ExpireMemoryTests.Expired_facts_are_deleted_by_the_hourly_service: fake clock. | Ph13 |
| TP-103 | MemoryBlocksTests.A_workflow_remembers_a_fact_and_a_later_run_recalls_it: two runs, same memory. | Ph13 |
| TP-104 | MemoryToolsTests.An_owner_session_reads_and_writes_the_same_store_over_MCP: McpGolden for tools/list. | Ph13 |
| TP-105 | ResourcePlannerTests.A_spec_with_a_database_and_a_disk_yields_env_names_and_two_rw_mounts: pure. | Ph14 |
| TP-106 | ResourceStoreTests.Provision_creates_owner_only_files_and_is_idempotent: modes 0700/0600, second call changes nothing. | Ph14 |
| TP-107 | DiskMeterTests.An_over_size_disk_fails_the_run_with_used_and_limit_numbers: SND-RES message has both figures. | Ph14 |
| TP-108 | DiskMeterTests.A_symlink_pointing_outside_the_folder_is_not_followed_or_counted: escape refused. | Ph14 |
| TP-109 | LimitsResolverTests.A_block_sandbox_resource_is_clamped_to_the_profile_and_box_limits: finding at validation, clamp at run. | Ph14 |
| TP-110 | PurgeResourcesTests.Archiving_a_workflow_keeps_its_data_and_purge_removes_it_only_when_inactive: running run blocks purge. | Ph14 |
| TP-111 | SandboxPlanGoldenTests.State_mounts_appear_in_the_rendered_plan_for_bubblewrap_and_Seatbelt: Golden/Sandbox regenerated. | Ph14 |
| TP-112 | ExampleSpecTests.The_example_spec_gets_its_state_database_and_sandbox_size: Part 5 done check. | Ph14 |
| TP-113 | ToolkitDefinitionTests.A_toolkit_serializes_its_name_needs_and_tool_schemas_to_the_manifest: golden `toolkit.json`. | Ph15 |
| TP-114 | ToolkitSchemaTests.The_input_schema_comes_from_the_request_record_and_marks_required_fields: nullable means optional. | Ph15 |
| TP-115 | ToolkitHostTests.A_stdio_client_lists_and_calls_a_tool_and_gets_a_typed_result: in-memory MCP transport. | Ph15 |
| TP-116 | ToolkitHostTests.A_thrown_exception_becomes_an_MCP_tool_error_without_the_stack_or_secrets: scrubbed message. | Ph15 |
| TP-117 | ToolkitResourcesTests.Database_cache_opens_the_file_named_by_the_SINDRI_DB_CACHE_variable: and fails clearly when missing. | Ph15 |
| TP-118 | NewToolkitGeneratorTests.A_valid_input_writes_the_project_a_test_and_its_own_build_rails: scaffold.test.sh case, no dotnet. | Ph15 |
| TP-119 | NewToolkitGeneratorTests.A_failed_build_reverts_every_file_it_wrote: NT revert rule. | Ph15 |
| TP-120 | ScaffoldE2eTests.A_scaffolded_toolkit_builds_with_warnaserror_and_its_sample_test_is_red_then_green: scripts/scaffold/e2e.sh. | Ph15 |
| TP-121 | ProcessSandboxPlannerTests.A_process_plan_mounts_only_declared_resources_and_opens_only_connection_hosts: pure, no bwrap. | Ph16 |
| TP-122 | BubblewrapProcessTests.Wrap_keeps_stdio_piped_and_adds_no_launch_secret: argv assertion with the fake processes. | Ph16 |
| TP-123 | BuildToolkitTests.A_toolkit_that_fails_warnaserror_or_its_tests_is_not_startable: SND-TK, state stays `failed`. | Ph16 |
| TP-124 | ToolkitSupervisorTests.A_crashing_toolkit_restarts_with_backoff_and_is_marked_failed_after_three_crashes: fake clock and process. | Ph16 |
| TP-125 | ToolkitToolsTests.tools_list_shows_running_toolkit_tools_and_hides_paused_and_off_ones: McpGolden. | Ph16 |
| TP-126 | ToolkitCallLogTests.Every_call_is_logged_with_caller_duration_and_a_scrubbed_capped_payload: secrets absent from the row. | Ph16 |
| TP-127 | CallToolBlockTests.A_workflow_calls_a_toolkit_tool_and_its_JSON_result_feeds_the_next_node: end to end with a fake toolkit. | Ph16 |
| TP-128 | ToolNamesTests.Names_use_one_separator_function_and_stay_within_the_client_limit: 64 chars, no dots if the fallback is chosen. | Ph16 |
| TP-129 | WorkflowsPageViewModelTests.The_list_shows_active_state_last_run_and_who_changed_it: `session:<id>` shown as text. | Ph17 |
| TP-130 | GraphCanvasTests.Nodes_and_edges_render_at_the_positions_the_server_returned_and_branch_labels_show: Avalonia headless. | Ph17 |
| TP-131 | RunDetailViewModelTests.A_node_with_truncated_output_says_so_and_a_for_each_shows_its_iterations: itemIndex grouping. | Ph17 |
| TP-132 | OpsEditorTests.Editing_a_config_field_sends_set_config_with_the_base_revision_and_a_conflict_prompts_a_reload: stale base path. | Ph17 |
| TP-133 | WorkflowGoldenContractTests.The_app_DTOs_read_every_workflow_golden_Sindri_serves: shared golden harness. | Ph17 |
| TP-134 | NeedsYouQuestionTests.Answering_an_ask_owner_question_posts_the_answer_and_clears_the_row: text and choice. | Ph17 |
| TP-135 | WorkflowsFramesTests.The_workflows_page_frame_matches_its_golden_in_light_and_dark: Ui.Frames.Tests. | Ph17 |
| TP-136 | WorkflowsUiEvidenceTests.The_seeded_fixture_screens_pass_the_ui_evidence_desktop_check: screens.json run through the recipe. | Ph17 |
| TP-137 | ConnectionsPageViewModelTests.The_list_shows_status_chips_and_a_needs_sign_in_row_offers_Sign_in_again: from API fixtures. | Ph18 |
| TP-138 | AddConnectionViewModelTests.Adding_an_oauth_connection_opens_the_consent_URL_and_polls_until_connected: fake launcher and API. | Ph18 |
| TP-139 | SecretFieldTests.A_pasted_key_is_sent_once_and_never_reaches_preferences_logs_or_view_model_state: scan after send. | Ph18 |
| TP-140 | MemoryPageViewModelTests.Search_shows_the_mode_and_a_forget_removes_the_row_after_the_API_confirms: keyword and semantic. | Ph18 |
| TP-141 | ToolkitsPageViewModelTests.Build_follows_the_job_and_shows_output_then_the_state_flips_to_built: fake job stream. | Ph18 |
| TP-142 | ResourcesPageViewModelTests.Purge_is_refused_with_the_reason_while_the_owner_is_active: SND-RES text shown. | Ph18 |
| TP-143 | ApiGoldenContractTests.The_app_DTOs_read_every_connection_memory_toolkit_and_resource_golden: shared harness. | Ph18 |
| TP-144 | ListPagesUiEvidenceTests.The_four_seeded_screens_pass_the_ui_evidence_desktop_check: screens.json run. | Ph18 |
