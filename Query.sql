use halik;

CREATE TABLE Users
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) UNIQUE NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
);

CREATE TABLE UserRankings
(
    UserId INT PRIMARY KEY,
    SingleElo INT NOT NULL DEFAULT 1000,
    MultiElo INT NOT NULL DEFAULT 1000,
    SingleRankedPlayed INT NOT NULL DEFAULT 0,
    SingleRankedWins INT NOT NULL DEFAULT 0,
    MultiRankedPlayed INT NOT NULL DEFAULT 0,
    MultiRankedWins INT NOT NULL DEFAULT 0,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

CREATE TABLE RankedMatches
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Mode NVARCHAR(20) NOT NULL, -- 'single' / 'multi'
    PlayedAt DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE RankedMatchResults
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    MatchId INT NOT NULL,
    UserId INT NOT NULL,
    Score INT NOT NULL,
    Placement INT NOT NULL,
    EloBefore INT NOT NULL,
    EloAfter INT NOT NULL,
    FOREIGN KEY (MatchId) REFERENCES RankedMatches(Id),
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

ALTER TABLE Users
ADD AvatarKey NVARCHAR(50) NOT NULL
CONSTRAINT DF_Users_AvatarKey DEFAULT 'default_1';

CREATE UNIQUE INDEX IX_Users_Username_CI
ON Users (Username);

SELECT * FROM Users