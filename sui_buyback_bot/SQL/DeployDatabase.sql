CREATE DATABASE IF NOT EXISTS sui;

USE sui;

CREATE TABLE IF NOT EXISTS users (
                                     id INT NOT NULL AUTO_INCREMENT,
                                     registered_by NVARCHAR(255) NOT NULL,
                                     paycheck_name NVARCHAR(255) NOT NULL,
                                     created_date_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
                                     updated_date_time TIMESTAMP NOT NULL DEFAULT  CURRENT_TIMESTAMP(3)
                                         ON UPDATE CURRENT_TIMESTAMP(3),
                                     status VARCHAR(25),
                                     privileges NVARCHAR(255),
                                     buyback_rate DOUBLE NULL,
                                     balance BIGINT NOT NULL DEFAULT 0,
                                     PRIMARY KEY (id, registered_by),
                                     UNIQUE KEY registered_by (registered_by),
                                     UNIQUE KEY paycheck_name (paycheck_name)
);

CREATE TABLE IF NOT EXISTS payments (
                                        payment_id INT NOT NULL AUTO_INCREMENT,
                                        registered_by NVARCHAR(255) NOT NULL,
                                        created_date_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
                                        updated_date_time TIMESTAMP NOT NULL DEFAULT  CURRENT_TIMESTAMP(3)
                                            ON UPDATE CURRENT_TIMESTAMP(3),
                                        status VARCHAR(10) DEFAULT 'NEW',
                                        payment_number BIGINT NOT NULL DEFAULT 0,
                                        PRIMARY KEY (payment_id),
                                        FOREIGN KEY (registered_by) REFERENCES users(registered_by)
);

CREATE TABLE IF NOT EXISTS fleets (
                                      id INT NOT NULL AUTO_INCREMENT,
                                      fleet_started_by NVARCHAR(255) NOT NULL,
                                      status VARCHAR(10) DEFAULT 'NEW',
                                      fleet_started DATETIME NULL NOT NULL DEFAULT  CURRENT_TIMESTAMP(3),
                                      fleet_ended DATETIME NULL ON UPDATE CURRENT_TIMESTAMP(3),
                                      PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS input_data_fleets (
                                                 id INT NOT NULL,
                                                 account_number INT NOT NULL,
                                                 created_date_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
                                                 updated_date_time TIMESTAMP NOT NULL DEFAULT  CURRENT_TIMESTAMP(3)
                                                     ON UPDATE CURRENT_TIMESTAMP(3),
                                                 item_name NVARCHAR(255) NOT NULL,
                                                 item_quantity DOUBLE NOT NULL,
                                                 status VARCHAR(10) DEFAULT 'NEW',
                                                 FOREIGN KEY (id) references fleets(id),
                                                 FOREIGN KEY (account_number) REFERENCES users(id)

);

CREATE TABLE IF NOT EXISTS input_data (
                                          id BIGINT NOT NULL AUTO_INCREMENT,
                                          created_by NVARCHAR(255) NOT NULL,
                                          account_number INT NOT NULL,
                                          created_date_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
                                          updated_date_time TIMESTAMP NOT NULL DEFAULT  CURRENT_TIMESTAMP(3)
                                              ON UPDATE CURRENT_TIMESTAMP(3),
                                          status VARCHAR(10) DEFAULT 'NEW',
                                          input_item_name NVARCHAR(255) NOT NULL,
                                          input_item_quantity DOUBLE NOT NULL,
                                          PRIMARY KEY (id),
                                          FOREIGN KEY (account_number) REFERENCES users(id)
);


CREATE TABLE IF NOT EXISTS processed_data (
                                              id BIGINT NOT NULL AUTO_INCREMENT,
                                              created_by NVARCHAR(255) NOT NULL,
                                              account_number INT NOT NULL,
                                              created_date_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
                                              updated_date_time TIMESTAMP NOT NULL DEFAULT  CURRENT_TIMESTAMP(3)
                                                  ON UPDATE CURRENT_TIMESTAMP(3),
                                              status VARCHAR(10) DEFAULT 'NEW',
                                              item_name NVARCHAR(255),
                                              item_quantity BIGINT,
                                              item_price BIGINT,
                                              PRIMARY KEY (id),
                                              FOREIGN KEY (account_number) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS transactions (
                                            id BIGINT NOT NULL AUTO_INCREMENT,
                                            created_by NVARCHAR(255) NOT NULL,
                                            account_number_from INT NOT NULL,
                                            account_number_to INT NOT NULL,
                                            created_timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
                                            amount BIGINT NOT NULL,
                                            balance_after BIGINT NOT NULL,
                                            PRIMARY KEY (id),
                                            FOREIGN KEY (account_number_from) REFERENCES  users(id),
                                            FOREIGN KEY (account_number_to) REFERENCES  users(id)
);

CREATE TABLE IF NOT EXISTS settings (
                                        created_date_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
                                        updated_date_time TIMESTAMP NOT NULL DEFAULT  CURRENT_TIMESTAMP(3),
                                        status VARCHAR(10) DEFAULT 'ACTIVE',
                                        settings_key NVARCHAR(255) NOT NULL,
                                        settings_value NVARCHAR(255) NOT NULL,
                                        settings_description TEXT,
                                        PRIMARY KEY (settings_key)
);

INSERT IGNORE INTO settings(settings_key, settings_value, settings_description) VALUES ('BUYBACK_GLOBAL_RATE', '0.85', 'Needs to be in 0.% format.');
INSERT IGNORE INTO settings(settings_key, settings_value, settings_description) VALUES ('BUYBACK_MIN_DAYS', '-7', 'Needs to be numeric value <0;-X>');
INSERT IGNORE INTO settings(settings_key, settings_value, settings_description) VALUES ('JF_BASIC_MOVE_PRICE', '1000', 'Needs to be numeric value <0;X>');

# UNCOMMENT below if you are not doing migration but first installation
#INSERT IGNORE INTO users(registered_by, paycheck_name, status, privileges, balance)
#VALUES ('SUI', 'SUI', 'ACTIVE', 'BOT', 0);

# BEGIN ALTERS