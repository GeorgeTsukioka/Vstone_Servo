/*--------------------------------------------------*/
/* @Program name ; sample1.cpp                      */
/* @Author : George Tsukioka                        */
/* @Comment : Check Vstone servo                    */
/*                                                  */
/* Copyright(c) 2016 George Tsukioka                */
/*--------------------------------------------------*/

#include <boost/asio.hpp>
#include <boost/thread.hpp>
#include <iostream>

using namespace boost::asio;
using namespace std;

int main(int argc, char *argv[])
{
	const char *PORT = "/dev/ttyUSB0";
	const int SID = 0x00;

	try {
		io_service io;
		serial_port port( io, PORT );
		port.set_option(serial_port_base::baud_rate(115200));
		port.set_option(serial_port_base::character_size(8));
		port.set_option(serial_port_base::flow_control(serial_port_base::flow_control::none));
		port.set_option(serial_port_base::parity(serial_port_base::parity::none));
		port.set_option(serial_port_base::stop_bits(serial_port_base::stop_bits::one));

		while(1){
			unsigned char buf[256];
			unsigned long bytes;
			int cmd;
			
			// unlock
			buf[0] = 0x80 | SID;
			buf[1] = 0x40 | 0x00 | 1;                                // write 1byte
			buf[2] = 0x14;                                           // SYS_ULK
			buf[3] = 0x55;
			port.write_some(buffer(buf));
			
			//menu
			std::cerr << "SID:" << SID << " PORT:" << PORT << std::endl
					  << "  1 .. motor ON" << std::endl
					  << "  2 .. motor OFF" << std::endl
					  << "  3 .. move to 0x400" << std::endl
					  << "  4 .. move to 0x800" << std::endl
					  << "  5 .. swing" << std::endl
					  << "  9 .. exit" << std::endl
					  << ">" ;
					  
			cin >> cmd;
			std::cerr << "cmd:" << cmd << std::endl;
			
			switch(cmd){
				
				case 1:
				case 2:
					buf[0] = 0x80 | SID;
					buf[1] = 0x40 | 0x00 | 1;                              // write 1byte
					buf[2] = 0x3b;                                         // PWM_EN
					buf[3] = (cmd==1) ? 1 : 0;
					port.write_some(buffer(buf));
					break;
				
				case 3:
				case 4:
					buf[0] = 0x80 | SID;
					buf[1] = 0x40 | 0x00 | 2;                              // write 2byte
					buf[2] = 0x30;                                         // FB_TPOS
					buf[3] = 0x00;                                         // low  byte
					buf[4] = (cmd==3) ? (0x400>>7) : (0x800>>7);           // high byte
					port.write_some(buffer(buf));
					break;
				
				case 5:
					int t;
					for(t=0; t<300; t++) {
						int tpos = (int)(0x800 + 0x400 * sin(3.1416*2*t/50));
						int k = 0, i;
						for(i=0; i<30; i++) {
							buf[k++] = 0xc0 | i;                               // burst
							buf[k++] = (tpos>>0) & 0x7f;
							buf[k++] = (tpos>>7) & 0x7f;
							buf[k++] = 1;
							buf[k++] = 0;
						}
						
						buf[k++] = 0x80 | 0x3f;                              // write broadcast
						buf[k++] = 0x40 | 1;
						buf[k++] = 0x4f;
						buf[k++] = 1;
						port.write_some(buffer(buf));
					}
					break;
					
				case 9:
					port.close();
					std::cerr << "exit" << std::endl;
					exit(0);
					break;
			}
		}
	}
	
	catch (const std::exception& e) {
		std::cerr << e.what() << std::endl;
		exit(EXIT_FAILURE);
	}

	return 0;
}
