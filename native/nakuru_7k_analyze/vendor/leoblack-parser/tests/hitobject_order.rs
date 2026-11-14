use leoblack_parser::{parse_osu_mania_from_text, NoteType};

fn chart(objects: &[&str]) -> String {
    format!(
        "osu file format v14\n[General]\nMode:3\n[Difficulty]\nCircleSize:7\nOverallDifficulty:8\n[TimingPoints]\n0,500,4,2,0,100,1,0\n[HitObjects]\n{}\n",
        objects.join("\n")
    )
}

#[test]
fn reversed_and_adjacent_swapped_notes_match_chronological_input() {
    let a = "36,192,1000,1,0,0:0:0:0:";
    let b = "109,192,1100,1,0,0:0:0:0:";
    let c = "182,192,1200,1,0,0:0:0:0:";
    let expected = parse_osu_mania_from_text(&chart(&[a, b, c])).unwrap();
    assert_eq!(
        expected.notes.iter().map(|n| n.time).collect::<Vec<_>>(),
        vec![1000., 1100., 1200.]
    );
    assert_eq!(
        parse_osu_mania_from_text(&chart(&[c, b, a])).unwrap(),
        expected
    );
    assert_eq!(
        parse_osu_mania_from_text(&chart(&[a, c, b])).unwrap(),
        expected
    );
}

#[test]
fn unsorted_ln_keeps_heads_bodies_and_tails() {
    let hold = "36,192,1000,128,0,1500:0:0:0:0:";
    let middle = "109,192,1100,1,0,0:0:0:0:";
    let last = "182,192,1600,1,0,0:0:0:0:";
    let expected = parse_osu_mania_from_text(&chart(&[hold, middle, last])).unwrap();
    assert_eq!(
        expected.notes.iter().map(|n| n.time).collect::<Vec<_>>(),
        vec![1000., 1100., 1500., 1600.]
    );
    assert_eq!(expected.notes[0].data[0], NoteType::HoldHead);
    assert_eq!(expected.notes[1].data[0], NoteType::HoldBody);
    assert_eq!(expected.notes[2].data[0], NoteType::HoldTail);
    assert_eq!(
        parse_osu_mania_from_text(&chart(&[last, middle, hold])).unwrap(),
        expected
    );
}

#[test]
fn equal_time_objects_preserve_the_original_order() {
    let tap = "36,192,1000,1,0,0:0:0:0:";
    let hold = "36,192,1000,128,0,1500:0:0:0:0:";
    let later = "109,192,1600,1,0,0:0:0:0:";
    let tap_then_hold = parse_osu_mania_from_text(&chart(&[tap, hold, later])).unwrap();
    assert_eq!(
        parse_osu_mania_from_text(&chart(&[later, tap, hold])).unwrap(),
        tap_then_hold
    );
    let hold_then_tap = parse_osu_mania_from_text(&chart(&[hold, tap, later])).unwrap();
    assert_eq!(
        parse_osu_mania_from_text(&chart(&[later, hold, tap])).unwrap(),
        hold_then_tap
    );
}
