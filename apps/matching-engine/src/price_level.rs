use std::collections::VecDeque;

use crate::domain::Order;

#[derive(Debug, Clone)]
pub struct PriceLevel {
    price: u64,
    orders: VecDeque<Order>,
}

impl PriceLevel {
    pub fn new(price: u64) -> Self {
        Self {
            price,
            orders: VecDeque::new(),
        }
    }

    pub fn price(&self) -> u64 {
        self.price
    }

    pub fn push_back(&mut self, order: Order) {
        self.orders.push_back(order);
    }

    pub fn front_mut(&mut self) -> Option<&mut Order> {
        self.orders.front_mut()
    }

    pub fn pop_front(&mut self) -> Option<Order> {
        self.orders.pop_front()
    }

    pub fn is_empty(&self) -> bool {
        self.orders.is_empty()
    }

    pub fn total_quantity(&self) -> u64 {
        self.orders.iter().map(|order| order.remaining_quantity).sum()
    }

    pub fn order_count(&self) -> usize {
        self.orders.len()
    }
}
