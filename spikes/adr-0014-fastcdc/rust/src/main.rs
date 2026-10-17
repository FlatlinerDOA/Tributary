//! Prints the chunk lengths the fastcdc crate's v2020 chunker produces for a file.
//! Usage: cdc-ref <file> <min> <avg> <max>
use fastcdc::v2020::FastCDC;

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let data = std::fs::read(&args[1]).unwrap();
    let [min, avg, max] = [2, 3, 4].map(|i| args[i].parse::<u32>().unwrap());
    let lengths: Vec<String> = FastCDC::new(&data, min, avg, max).map(|c| c.length.to_string()).collect();
    println!("{}", lengths.join("\n"));
}
