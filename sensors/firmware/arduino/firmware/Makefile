# Project name and target
PROJECT = firmware
MCU = atmega328p
F_CPU = 16000000UL
BAUDRATE = 115200

# Platform defaults (override on command line as needed)
ifeq ($(OS),Windows_NT)
ARDUINO_PORT ?= COM3
ARDUINO_DIR ?= C:/Arduino
MKDIR = mkdir
RM = rmdir /S /Q
else
ARDUINO_PORT ?= /dev/ttyACM0
ARDUINO_DIR ?= /usr/share/arduino
MKDIR = mkdir -p
RM = rm -rf
endif

fixpath = $(subst /,\,$1)

# Compiler and tools
CC = avr-gcc
CXX = avr-g++
OBJCOPY = avr-objcopy
AVRDUDE = avrdude
SIZE = avr-size

# Directories
BUILD_DIR = ./bin
SRC_DIR = .
ARDUINO_CORE = $(ARDUINO_DIR)/hardware/arduino/avr/cores/arduino
ARDUINO_VARIANTS = $(ARDUINO_DIR)/hardware/arduino/avr/variants/standard

# Source files (add more as needed)
SRC = $(wildcard $(SRC_DIR)/*.cpp)
ARDUINO_SRC = $(wildcard $(ARDUINO_CORE)/*.cpp) $(wildcard $(ARDUINO_CORE)/*.c)
ARDUINO_OBJ = $(patsubst $(ARDUINO_CORE)/%.cpp,$(BUILD_DIR)/core/%.o,$(wildcard $(ARDUINO_CORE)/*.cpp)) \
	$(patsubst $(ARDUINO_CORE)/%.c,$(BUILD_DIR)/core/%.o,$(wildcard $(ARDUINO_CORE)/*.c))

# Include paths
INCLUDES = -I$(SRC_DIR) \
		   -I/usr/lib/avr/include \
		   -I$(ARDUINO_CORE) \
		   -I$(ARDUINO_VARIANTS) \
 		   -I$(ARDUINO_CORE_USER)

# Compiler flags
CFLAGS = -mmcu=$(MCU) -DF_CPU=$(F_CPU) -Os -Wall -std=c11 $(INCLUDES)
CXXFLAGS = -mmcu=$(MCU) -DF_CPU=$(F_CPU) -Os -Wall -std=c++11 $(INCLUDES)
LDFLAGS = -mmcu=$(MCU) -Wl,--gc-sections -Os

# AVRDUDE settings
AVRDUDE_FLAGS = -p $(MCU) -c arduino -P $(ARDUINO_PORT) -b $(BAUDRATE) -D

# Targets
.PHONY: all clean upload size

all: $(BUILD_DIR)/$(PROJECT).hex size

# Create build directories
ifeq ($(OS),Windows_NT)
$(BUILD_DIR):
	@if not exist "$(call fixpath,$(BUILD_DIR))" $(MKDIR) "$(call fixpath,$(BUILD_DIR))"
	@if not exist "$(call fixpath,$(BUILD_DIR)/core)" $(MKDIR) "$(call fixpath,$(BUILD_DIR)/core)"

# Clean build files
clean:
	@if exist "$(call fixpath,$(BUILD_DIR))" $(RM) "$(call fixpath,$(BUILD_DIR))"
else
$(BUILD_DIR):
	$(MKDIR) $(BUILD_DIR)
	$(MKDIR) $(BUILD_DIR)/core

# Clean build files
clean:
	$(RM) $(BUILD_DIR)
endif

# Compile C++ source files
$(BUILD_DIR)/%.o: $(SRC_DIR)/%.cpp | $(BUILD_DIR)
	$(CXX) $(CXXFLAGS) -c $< -o $@

# Compile Arduino core C++ files
$(BUILD_DIR)/core/%.o: $(ARDUINO_CORE)/%.cpp | $(BUILD_DIR)
	$(CXX) $(CXXFLAGS) -c $< -o $@

# Compile Arduino core C files
$(BUILD_DIR)/core/%.o: $(ARDUINO_CORE)/%.c | $(BUILD_DIR)
	$(CC) $(CFLAGS) -c $< -o $@

# Link the project
$(BUILD_DIR)/$(PROJECT).elf: $(patsubst $(SRC_DIR)/%.cpp,$(BUILD_DIR)/%.o,$(SRC)) $(ARDUINO_OBJ)
	$(CXX) $(LDFLAGS) $^ -o $@

# Generate hex file
$(BUILD_DIR)/$(PROJECT).hex: $(BUILD_DIR)/$(PROJECT).elf
	$(OBJCOPY) -O ihex -R .eeprom $< $@

# Upload to Arduino
upload: $(BUILD_DIR)/$(PROJECT).hex
	$(AVRDUDE) $(AVRDUDE_FLAGS) -U flash:w:$<:i

# Display firmware size
size: $(BUILD_DIR)/$(PROJECT).elf
	$(SIZE) --format=avr --mcu=$(MCU) $<