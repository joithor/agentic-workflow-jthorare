-- The workflow engine (factory addendum §17.28): instance rows, and which instance each workflow event belongs to.
-- An instance row is the fold of its events: snapshot_json holds the whole instance; the other columns are what
-- queries filter and sort on. version rises by one per event (optimistic concurrency); tenant and ring are the §17.20
-- seams. events.instance is null for every event that isn't a workflow event.
ALTER TABLE events ADD COLUMN instance TEXT;
CREATE INDEX events_instance_seq ON events (instance, seq) WHERE instance IS NOT NULL;
CREATE TABLE workflow_instances (
    id TEXT PRIMARY KEY,
    recipe TEXT NOT NULL,
    recipe_hash TEXT NOT NULL,
    item TEXT NOT NULL,
    attempt INTEGER NOT NULL CHECK (attempt >= 1),
    parent TEXT REFERENCES workflow_instances (id),
    tenant TEXT NOT NULL,
    ring INTEGER NOT NULL,
    version INTEGER NOT NULL CHECK (version >= 1),
    state TEXT NOT NULL,
    status TEXT NOT NULL,
    deadline TEXT,
    started_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    snapshot_json TEXT NOT NULL,
    UNIQUE (tenant, recipe, item, attempt)
);
CREATE INDEX workflow_instances_due ON workflow_instances (deadline) WHERE deadline IS NOT NULL;
CREATE INDEX workflow_instances_parent ON workflow_instances (parent) WHERE parent IS NOT NULL;
