DROP SCHEMA IF EXISTS staging CASCADE;
CREATE SCHEMA staging;

CREATE UNLOGGED TABLE staging.artist (
    id integer,
    gid uuid,
    name text,
    sort_name text,
    begin_year smallint,
    end_year smallint
);

CREATE UNLOGGED TABLE staging.artist_credit_name (
    artist_credit integer,
    position smallint,
    artist integer
);

CREATE UNLOGGED TABLE staging.release_group (
    id integer,
    gid uuid,
    name text,
    artist_credit integer,
    type integer
);

CREATE UNLOGGED TABLE staging.release_group_meta (
    id integer,
    first_release_year smallint
);

CREATE UNLOGGED TABLE staging.release_group_primary_type (
    id integer,
    name text
);

CREATE UNLOGGED TABLE staging.release (
    id integer,
    gid uuid,
    name text,
    artist_credit integer,
    release_group integer,
    status integer
);

CREATE UNLOGGED TABLE staging.release_status (
    id integer,
    name text
);

CREATE UNLOGGED TABLE staging.recording (
    id integer,
    gid uuid,
    name text,
    artist_credit integer,
    length integer
);

CREATE UNLOGGED TABLE staging.medium (
    id integer,
    release integer
);

CREATE UNLOGGED TABLE staging.track (
    recording integer,
    medium integer
);

CREATE UNLOGGED TABLE staging.genre (
    id integer,
    gid uuid,
    name text
);

CREATE UNLOGGED TABLE staging.tag (
    id integer,
    name text
);

CREATE UNLOGGED TABLE staging.artist_tag (
    artist integer,
    tag integer,
    count integer
);

CREATE UNLOGGED TABLE staging.release_group_tag (
    release_group integer,
    tag integer,
    count integer
);

CREATE UNLOGGED TABLE staging.recording_tag (
    recording integer,
    tag integer,
    count integer
);
