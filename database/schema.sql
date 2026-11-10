-- NextStep UA database schema (matches the team ERD)
-- MySQL 8.x. Run this FIRST on an empty database, then run seed.sql.
-- Re-running is safe: it drops and recreates every table (all data is reloaded by seed.sql).

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS resource_assignment_type, resource_subject, resource_course, schedule,
                     location, resource, course_example, assignment_type, course, subject;
SET FOREIGN_KEY_CHECKS = 1;

-- Subject areas from the UA catalog's alpha-symbol table (CH = Chemistry)
CREATE TABLE subject (
  subject_id INT NOT NULL PRIMARY KEY,
  code       VARCHAR(10)  NOT NULL UNIQUE,
  name       VARCHAR(120) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Every undergraduate course in the official UA 2026-27 catalog
CREATE TABLE course (
  course_id   INT NOT NULL PRIMARY KEY,
  subject_id  INT NOT NULL,
  number      VARCHAR(10)  NOT NULL,          -- text, so "005" and "101L" stay exact
  title       VARCHAR(200) NOT NULL,
  description TEXT NOT NULL,
  UNIQUE KEY uq_course_code (subject_id, number),
  FULLTEXT KEY ft_course_text (title, description),  -- keyword search to pick candidate courses for the AI
  CONSTRAINT fk_course_subject FOREIGN KEY (subject_id) REFERENCES subject(subject_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- The 7 assignment types the AI can return (name = the exact value the AI uses)
CREATE TABLE assignment_type (
  type_id     INT NOT NULL PRIMARY KEY,
  name        VARCHAR(30)  NOT NULL UNIQUE,
  description VARCHAR(255) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Short summaries of example assignments (never files). Empty for now.
CREATE TABLE course_example (
  example_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  course_id  INT NOT NULL,
  type_id    INT NOT NULL,
  summary    TEXT NOT NULL,
  keywords   VARCHAR(500) NULL,
  source     VARCHAR(255) NULL,
  CONSTRAINT fk_example_course FOREIGN KEY (course_id) REFERENCES course(course_id),
  CONSTRAINT fk_example_type   FOREIGN KEY (type_id)   REFERENCES assignment_type(type_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- A place to get help (e.g. "Learning Commons Peer Tutoring – CH 101", "University Writing Center")
CREATE TABLE resource (
  resource_id    INT NOT NULL PRIMARY KEY,
  name           VARCHAR(200) NOT NULL,
  description    TEXT NULL,                 -- what it helps with, from the team's research
  what_to_expect TEXT NULL,
  contact_email  VARCHAR(120) NULL,
  contact_phone  VARCHAR(40)  NULL,
  booking_url    VARCHAR(500) NULL,
  source_url     VARCHAR(500) NULL,
  eligibility    VARCHAR(255) NULL,         -- NULL = open to all students
  is_general     BOOLEAN NOT NULL DEFAULT FALSE,  -- TRUE = fallback for any course
  last_verified  DATE NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- A building + room, or online
CREATE TABLE location (
  location_id INT NOT NULL PRIMARY KEY,
  building    VARCHAR(150) NOT NULL,
  room        VARCHAR(50)  NULL,
  is_online   BOOLEAN NOT NULL DEFAULT FALSE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- When a resource is open. Drop-in vs. appointment is per time slot because it changes by day.
CREATE TABLE schedule (
  schedule_id    INT NOT NULL PRIMARY KEY,
  resource_id    INT NOT NULL,
  location_id    INT NULL,                  -- NULL = room not published
  day_of_week    VARCHAR(10) NULL,          -- NULL = by appointment, no fixed day
  start_time     TIME NULL,                 -- NULL = hours vary / not published
  end_time       TIME NULL,
  session_type   ENUM('drop_in','appointment') NOT NULL,
  session_detail VARCHAR(100) NULL,         -- the original wording, e.g. "SI group session"
  start_date     DATE NULL,                 -- only for slots limited to a date range
  end_date       DATE NULL,
  notes          VARCHAR(500) NULL,
  CONSTRAINT fk_schedule_resource FOREIGN KEY (resource_id) REFERENCES resource(resource_id),
  CONSTRAINT fk_schedule_location FOREIGN KEY (location_id) REFERENCES location(location_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Which courses a resource covers
CREATE TABLE resource_course (
  resource_id INT NOT NULL,
  course_id   INT NOT NULL,
  PRIMARY KEY (resource_id, course_id),
  CONSTRAINT fk_rc_resource FOREIGN KEY (resource_id) REFERENCES resource(resource_id),
  CONSTRAINT fk_rc_course   FOREIGN KEY (course_id)   REFERENCES course(course_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Which whole subjects a resource covers (empty for now; kept for the fallback chain)
CREATE TABLE resource_subject (
  resource_id INT NOT NULL,
  subject_id  INT NOT NULL,
  PRIMARY KEY (resource_id, subject_id),
  CONSTRAINT fk_rs_resource FOREIGN KEY (resource_id) REFERENCES resource(resource_id),
  CONSTRAINT fk_rs_subject  FOREIGN KEY (subject_id)  REFERENCES subject(subject_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Which assignment types a resource helps with
CREATE TABLE resource_assignment_type (
  resource_id INT NOT NULL,
  type_id     INT NOT NULL,
  PRIMARY KEY (resource_id, type_id),
  CONSTRAINT fk_rat_resource FOREIGN KEY (resource_id) REFERENCES resource(resource_id),
  CONSTRAINT fk_rat_type     FOREIGN KEY (type_id)     REFERENCES assignment_type(type_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
