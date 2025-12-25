#!/usr/bin/env python3
"""
Kafka Producer Demo Script

This script sends test messages to a Kafka topic to validate Kafka functionality.
"""

import json
import time
import sys
from datetime import datetime
from kafka import KafkaProducer
from kafka.errors import KafkaError
import argparse
import logging

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)


class KafkaProducerDemo:
    def __init__(self, bootstrap_servers, topic_name):
        self.bootstrap_servers = bootstrap_servers
        self.topic_name = topic_name
        self.producer = None
        
    def connect(self):
        """Initialize Kafka producer connection"""
        try:
            self.producer = KafkaProducer(
                bootstrap_servers=self.bootstrap_servers,
                value_serializer=lambda x: json.dumps(x).encode('utf-8'),
                key_serializer=lambda x: x.encode('utf-8') if x else None,
                acks='all',  # Wait for all replicas to acknowledge
                retries=3,
                batch_size=16384,
                linger_ms=10,
                buffer_memory=33554432
            )
            logger.info(f"Connected to Kafka at {self.bootstrap_servers}")
            return True
        except Exception as e:
            logger.error(f"Failed to connect to Kafka: {e}")
            return False
    
    def send_test_messages(self, num_messages=10, interval=1):
        """Send test messages to the Kafka topic"""
        if not self.producer:
            logger.error("Producer not initialized. Call connect() first.")
            return False
        
        successful_sends = 0
        failed_sends = 0
        
        for i in range(num_messages):
            try:
                message = {
                    "message_id": i + 1,
                    "timestamp": datetime.now().isoformat(),
                    "content": f"Test message {i + 1}",
                    "producer": "kafka-demo-producer",
                    "metadata": {
                        "test_run": True,
                        "sequence": i + 1,
                        "total_messages": num_messages
                    }
                }
                
                # Send message with a key based on message_id for partitioning
                key = f"msg_{i + 1}"
                
                future = self.producer.send(
                    self.topic_name, 
                    value=message, 
                    key=key
                )
                
                # Block for 'synchronous' sends to get immediate feedback
                record_metadata = future.get(timeout=10)
                
                logger.info(f"Message {i + 1} sent successfully to partition {record_metadata.partition} at offset {record_metadata.offset}")
                successful_sends += 1
                
                if interval > 0 and i < num_messages - 1:
                    time.sleep(interval)
                    
            except KafkaError as e:
                logger.error(f"Failed to send message {i + 1}: {e}")
                failed_sends += 1
            except Exception as e:
                logger.error(f"Unexpected error sending message {i + 1}: {e}")
                failed_sends += 1
        
        # Ensure all messages are sent
        self.producer.flush()
        
        logger.info(f"Batch complete: {successful_sends} successful, {failed_sends} failed")
        return successful_sends > 0
    
    def send_custom_message(self, message_content, key=None):
        """Send a custom message to the Kafka topic"""
        if not self.producer:
            logger.error("Producer not initialized. Call connect() first.")
            return False
        
        try:
            message = {
                "timestamp": datetime.now().isoformat(),
                "content": message_content,
                "producer": "kafka-demo-producer",
                "custom": True
            }
            
            future = self.producer.send(
                self.topic_name,
                value=message,
                key=key
            )
            
            record_metadata = future.get(timeout=10)
            logger.info(f"Custom message sent to partition {record_metadata.partition} at offset {record_metadata.offset}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to send custom message: {e}")
            return False
    
    def close(self):
        """Close the producer connection"""
        if self.producer:
            self.producer.close()
            logger.info("Producer connection closed")


def main():
    parser = argparse.ArgumentParser(description='Kafka Producer Demo Script')
    parser.add_argument('--bootstrap-servers', default='10.10.100.93:9092',
                       help='Kafka bootstrap servers (default: localhost:9092)')
    parser.add_argument('--topic', default='test-topic',
                       help='Kafka topic name (default: test-topic)')
    parser.add_argument('--messages', type=int, default=10,
                       help='Number of test messages to send (default: 10)')
    parser.add_argument('--interval', type=float, default=1.0,
                       help='Interval between messages in seconds (default: 1.0)')
    parser.add_argument('--custom-message',
                       help='Send a single custom message instead of test batch')
    parser.add_argument('--key',
                       help='Message key for custom message')
    
    args = parser.parse_args()
    
    # Create producer instance
    producer_demo = KafkaProducerDemo(args.bootstrap_servers, args.topic)
    
    # Connect to Kafka
    if not producer_demo.connect():
        logger.error("Failed to connect to Kafka. Exiting.")
        sys.exit(1)
    
    try:
        if args.custom_message:
            # Send single custom message
            success = producer_demo.send_custom_message(args.custom_message, args.key)
        else:
            # Send test message batch
            success = producer_demo.send_test_messages(args.messages, args.interval)
        
        if success:
            logger.info("Producer demo completed successfully!")
        else:
            logger.error("Producer demo failed!")
            sys.exit(1)
            
    except KeyboardInterrupt:
        logger.info("Producer demo interrupted by user")
    except Exception as e:
        logger.error(f"Unexpected error: {e}")
        sys.exit(1)
    finally:
        producer_demo.close()


if __name__ == "__main__":
    main()