using UnityEngine;
using System.Collections;
using System.Collections.Generic;

//this is an enum, which defines a type of variable that only has a few
// possible named values. The CardState variable type has one of four values:
//drawpile, tableau, target, & discard
public enum CardState {
	drawpile,
	tableau,
	target,
	discard
}

public class CardProspector : Card {
	//this is how you use the enum CardState
	public CardState			state = CardState.drawpile;
	//the hiddenBy list stores which other cards will keep this one face down
	public List<CardProspector>	hiddenBy = new List<CardProspector> ();
	//LayoutID matches this card to a layout cml id if its a tableau card
	public int					layoutID;
	//the SlotDef class stores information pulled in from the LayoutXML <slot>
	public SlotDef				slotDef;

	//this allows the card to react to being clicked
	override public void OnMouseUpAsButton() {
		Prospector.S.CardClicked (this);
		base.OnMouseUpAsButton ();
	}
}