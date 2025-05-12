EESchema Schematic File Version 4
EELAYER 29 0
EELAYER END
$Descr A4 11693 8268
encoding utf-8
Sheet 1 1
Title "FeltSight Flash Storage"
Date "2024"
Rev ""
Comp ""
Comment1 ""
Comment2 ""
Comment3 ""
Comment4 ""
$EndDescr
$Comp
L nRF52840 nRF52840
U 1 1 5F0F0F0F
P 2000 2000
F 0 "nRF52840" H 2000 2100 50  0000 C CNN
F 1 "nRF52840" H 2000 1900 50  0000 C CNN
F 2 "" H 2000 2000 50  0000 C CNN
F 3 "" H 2000 2000 50  0000 C CNN
	1    2000 2000
	1    0    0    -1
$EndComp
$Comp
L W25Q128 W25Q128
U 1 1 5F0F0F0F
P 3000 2000
F 0 "W25Q128" H 3000 2100 50  0000 C CNN
F 1 "W25Q128" H 3000 1900 50  0000 C CNN
F 2 "" H 3000 2000 50  0000 C CNN
F 3 "" H 3000 2000 50  0000 C CNN
	1    3000 2000
	1    0    0    -1
$EndComp
Wire Wire Line
	2000 2000 3000 2000
Text Notes 2000 2500 0    50   ~ 0
Adafruit nRF52840
Text Notes 3000 2500 0    50   ~ 0
16MB Flash Memory
$EndSCHEMATC 