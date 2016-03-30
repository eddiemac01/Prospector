using UnityEngine;
using System.Collections;
using System.Collections.Generic;

//An enum to handle all the possible scoring events
public enum ScoreEvent {
	draw,
	mine,
	mineGold,
	gameWin,
	gameLoss
}

public class Prospector : MonoBehaviour {
	static public Prospector 	S;
	static public int			SCORE_FROM_PREV_ROUND = 0;
	static public int			HIGH_SCORE = 0;

	public float				reloadDelay = 1f;

	public Vector3				fsPosMid = new Vector3 (0.5f, 0.90f, 0);
	public Vector3				fsPosRun = new Vector3 (0.5f, 0.75f, 0);
	public Vector3				fsPosMid2 = new Vector3 (0.5f, 0.5f, 0);
	public Vector3				fsPosEnd = new Vector3 (1.0f, 0.65f, 0);

	public Deck					deck;
	public TextAsset			deckXML;

	public Layout				layout;
	public TextAsset			layoutXML;
	public Vector3				layoutCenter;
	public float				xOffset = 3;
	public float				yOffset = -2.5f;
	public Transform			layoutAnchor;

	public CardProspector		target;
	public List<CardProspector>	tableau;
	public List<CardProspector>	discardPile;

	void Awake(){
		S = this;
		//Check for a high score in PlayerPrefs
		if (PlayerPrefs.HasKey ("ProspectorHighScore")) {
			HIGH_SCORE = PlayerPrefs.GetInt ("ProspectorHighScore");
		}
		//Add the score from last round, which will be >0 if it was a win
		score += SCORE_FROM_PREV_ROUND;
		//and reset
		SCORE_FROM_PREV_ROUND = 0;

		//Set up the Guitexts that show at the end of the round
		GameObject go = GameObject.Find ("GameOver");
		if (go != null) {
			GTGameOver = go.GetComponent<GUIText> ();
		}
		go = GameObject.Find ("RoundResult");
		if (go != null) {
			GTRoundResult = go.GetComponent<GUIText> ();
		}
		//Make them invisible
		ShowResultGTs (false);

		go = GameObject.Find ("HighScore");
		string hScore = "High Score: " + Utils.AddCommasToNumber (HIGH_SCORE);
		go.GetComponent<GUIText> ().text = hScore;
	}

	void ShowResultGTs(bool show) {
		GTGameOver.gameObject.SetActive (show);
		GTRoundResult.gameObject.SetActive (show);
	}

	public List<CardProspector>		drawPile;

	//Fields to track score info
	public int						chain = 0;
	public int						scoreRun = 0;
	public int						score = 0;
	public FloatingScore			fsRun;

	public GUIText					GTGameOver;
	public GUIText					GTRoundResult;

	void Start() {
		Scoreboard.S.score = score;

		deck = GetComponent<Deck> ();
		deck.InitDeck (deckXML.text);
		Deck.Shuffle (ref deck.cards);

		layout = GetComponent<Layout> ();	//get the layout
		layout.ReadLayout (layoutXML.text);	//pass LayoutXML to it

		drawPile = ConvertListCardsToListCardProspectors (deck.cards);
		LayoutGame ();
	}

	//The Draw function will pull a single card from the drawpile and return it
	CardProspector Draw () {
		CardProspector cd = drawPile [0];	//pull the oth cardprospector
		drawPile.RemoveAt (0);				//then remove it from List<> drawPile
		return(cd);							//and return it
	}

	//Convert from the layoutID int to the cardprospector with that id
	CardProspector FindCardByLayoutID(int layoutID) {
		foreach (CardProspector tCP in tableau) {
			//search through all cards in the tableau list<>
			if (tCP.layoutID == layoutID) {
				//if the card has the same id, return it
				return(tCP);
			}
		}
		//if its not found, return null
		return(null);
	}

	void LayoutGame() {
		//create an empty GameObject to serve as an anchor for the tableau
		if (layoutAnchor == null) {
			GameObject tGO = new GameObject ("_LayoutAnchor");
			// ^^ create an empty gameobject named _LayoutAnchor in the hierarchy
			layoutAnchor = tGO.transform;			//grab its transform
			layoutAnchor.transform.position = layoutCenter;
		}

		CardProspector cp;
		//Follow the layout
		foreach (SlotDef tSD in layout.slotDefs) {
			// itherate through all the slotdefs in the layou.slotdefs as tSD
			cp = Draw ();
			cp.faceUP = tSD.faceUp;
			cp.transform.parent = layoutAnchor;
			cp.transform.localPosition = new Vector3 (
				layout.multiplier.x * tSD.x,
				layout.multiplier.y * tSD.y,
				-tSD.layerID);
			cp.layoutID = tSD.id;
			cp.slotDef = tSD;
			cp.state = CardState.tableau;

			cp.SetSortingLayerName(tSD.layerName); //set sorting layers

			tableau.Add (cp);
		}

		//Set which cards are hiding others
		foreach (CardProspector tCP in tableau) {
			foreach (int hid in tCP.slotDef.hiddenBy) {
				cp = FindCardByLayoutID (hid);
				tCP.hiddenBy.Add (cp);
			}
		}

		//Set up the initial target card
		MoveToTarget (Draw ());

		//Set up the Draw Pile
		UpdateDrawPile ();
	}

	List<CardProspector> ConvertListCardsToListCardProspectors(List<Card> lCD) {
		List<CardProspector> lCP = new List<CardProspector> ();
		CardProspector tCP;
		foreach (Card tCD in lCD) {
			tCP = tCD as CardProspector;
			lCP.Add (tCP);
		}
		return(lCP);
	}

	//CardClicked is called any time a card in the game is clicked
	public void CardClicked(CardProspector cd) {
		//The reaction is determined by the state of the clicked card
		switch (cd.state) {
		case CardState.target:
			//clicking the target card does nothing
			break;
		case CardState.drawpile:
			//Clicking any card in the drawpile will draw the next card
			MoveToDiscard (target);
			MoveToTarget (Draw ());
			UpdateDrawPile ();
			ScoreManager(ScoreEvent.draw);
			break;
		case CardState.tableau:
			//Clicking a card in the tableau will check if its a valid play
			bool validMatch = true;
			if (!cd.faceUP){
				validMatch = false;
			}
			if (!AdjacentRank(cd, target)) {
				validMatch = false;
			}
			if (!validMatch) return;
			tableau.Remove (cd);
			MoveToTarget(cd);
			SetTableauFaces(); //update tablau card faceups
			ScoreManager(ScoreEvent.mine);
			break;
		}
		//Check to see whether the game is over or not
		CheckForGameOver ();
	}

	//Moves the current target to the discardPile
	void MoveToDiscard(CardProspector cd) {
		//Set the state of the card to discard
		cd.state = CardState.discard;
		discardPile.Add (cd);
		cd.transform.parent = layoutAnchor;
		cd.transform.localPosition = new Vector3 (
			layout.multiplier.x * layout.discardPile.x,
			layout.multiplier.y * layout.discardPile.y,
			-layout.discardPile.layerID + 0.5f);
		//Position it on the discardPile
		cd.faceUP = true;
		cd.SetSortingLayerName (layout.discardPile.layerName);
		cd.SetSortOrder (-100 + discardPile.Count);
	}

	//Make cd the new target card
	void MoveToTarget(CardProspector cd) {
		//If there is currently a target card, move it to discard pile
		if (target != null)
			MoveToDiscard (target);
		target = cd;
		cd.state = CardState.target;
		cd.transform.parent = layoutAnchor;
		//Move to the target position
		cd.transform.localPosition = new Vector3 (
			layout.multiplier.x * layout.discardPile.x,
			layout.multiplier.y * layout.discardPile.y,
			-layout.discardPile.layerID);
		cd.faceUP = true;
		cd.SetSortingLayerName (layout.discardPile.layerName);
		cd.SetSortOrder (0);
	}

	//Arranges all the cards of the drawpile to show how many are left
	void UpdateDrawPile() {
		CardProspector cd;
		//Go through all the cards of the draw pile
		for (int i=0; i<drawPile.Count; i++) {
			cd = drawPile [i];
			cd.transform.parent = layoutAnchor;
			Vector2 dpStagger = layout.drawPile.stagger;
			cd.transform.localPosition = new Vector3 (
				layout.multiplier.x * (layout.drawPile.x + i * dpStagger.x),
				layout.multiplier.y * (layout.drawPile.y + i * dpStagger.y),
				-layout.drawPile.layerID + 0.1f * i);
			cd.faceUP = false;
			cd.state = CardState.drawpile;
			cd.SetSortingLayerName (layout.drawPile.layerName);
			cd.SetSortOrder (-10 * i);
		}
	}

	//Return true if the two cards are adjacent in rank ( A + K wraparound)
	public bool AdjacentRank(CardProspector c0, CardProspector c1) {
		//If either card is face-down, its not adjacent
		if (!c0.faceUP || !c1.faceUP)
			return(false);

		//If they are 1 apart, they are adjacent
		if (Mathf.Abs (c0.rank - c1.rank) == 1) {
			return(true);
		}
		//If one is A and the other King, theyre adjacent
		if (c0.rank == 1 && c1.rank == 13)
			return(true);
		if (c0.rank == 13 && c1.rank == 1)
			return(true);
		return(false);
	}

	//This turns cards in the mine faceup or face down
	void SetTableauFaces() {
		foreach (CardProspector cd in tableau) {
			bool fup = true;
			foreach (CardProspector cover in cd.hiddenBy) {
				//if either of the covering cards are in the tableau
				if (cover.state == CardState.tableau) {
					fup = false; //then this card is facedown
				}
			}
			cd.faceUP = fup;
		}
	}

	//Test whether the game is over
	void CheckForGameOver() {
		//If the tableau is empty, the game is over
		if (tableau.Count == 0) {
			//Call Gameover() with a win
			GameOver (true);
			return;
		}
		//if there are still cards in the draw pile, the games not over
		if (drawPile.Count > 0) {
			return;
		}
		//Check for remaining valid plays
		foreach (CardProspector cd in tableau) {
			if (AdjacentRank (cd, target)) {
				//if there is a valid play, the games not over
				return;
			}
		}
		//Since there are no valid plays, the game is over
		//call GamOver with a loss
		GameOver (false);
	}

	//Called when the game is over. Simple for now, but expandable
	void GameOver(bool won) {
		if (won) {
			ScoreManager(ScoreEvent.gameWin);
		} else {
			ScoreManager(ScoreEvent.gameLoss);
		}
		//Reload the scene, resetting the game
		Invoke ("ReloadLevel", reloadDelay);
		//Application.LoadLevel ("__Prospector_Scene_0");
	}

	void ReloadLevel() {
		//Reload the scene, resetting the game
		Application.LoadLevel ("__Prospector_Scene_0");
	}

	//ScoreManager handles all of the scoring
	void ScoreManager(ScoreEvent sEvt) {
		List<Vector3> fsPts;
		switch (sEvt) {
		case ScoreEvent.draw:
		case ScoreEvent.gameWin:
		case ScoreEvent.gameLoss:
			chain = 0;
			score += scoreRun;
			scoreRun = 0;
			//Add fsRun to the _Scoreboard score
			if (fsRun != null) {
				//Create points for the Bezier curve
				fsPts = new List<Vector3>();
				fsPts.Add (fsPosRun);
				fsPts.Add (fsPosMid2);
				fsPts.Add (fsPosEnd);
				fsRun.reportFinishTo = Scoreboard.S.gameObject;
				fsRun.Init (fsPts, 0, 1);
				//Also adjust the fontSize
				fsRun.fontSizes = new List<float>(new float[] {28,36,4});
				fsRun = null; //Clear fsRun so its created again
			}
			break;
		case ScoreEvent.mine:
			chain++;
			scoreRun += chain;
			//Create a FloatingScore for this score
			FloatingScore fs;
			//Move it from the mouseposition to fsPosRun
			Vector3 p0 = Input.mousePosition;
			p0.x /= Screen.width;
			p0.y /= Screen.height;
			fsPts = new List<Vector3>();
			fsPts.Add (p0);
			fsPts.Add (fsPosMid);
			fsPts.Add (fsPosRun);
			fs = Scoreboard.S.CreateFloatingScore(chain, fsPts);
			fs.fontSizes = new List<float>(new float[] {4, 50, 28});
			if (fsRun == null) {
				fsRun = fs;
				fsRun.reportFinishTo = null;
			} else {
				fs.reportFinishTo = fsRun.gameObject;
			}
			break;
		}

		//This second switch statement handles round wins and losses
		switch (sEvt) {
		case ScoreEvent.gameWin:
			GTGameOver.text = "Round Over";
			Prospector.SCORE_FROM_PREV_ROUND = score;
			print ("You won this round! Round score: " + score);
			GTRoundResult.text = "You won this round!\nRound Score: "+score;
			ShowResultGTs(true);
			break;
		case ScoreEvent.gameLoss:
			GTGameOver.text = "Game Over";
			if (Prospector.HIGH_SCORE <= score) {
				print ("You got the high score! High score: " + score);
				string sRR = "You got the high score!\nHigh score: "+score;
				GTRoundResult.text = sRR;
				Prospector.HIGH_SCORE = score;
				PlayerPrefs.SetInt ("ProspectorHighScore", score);
			} else {
				print ("Your final score for the game was: " + score);
				GTRoundResult.text = "Your final score was: "+score;
			}
			ShowResultGTs(true);
			break;
		default:
			print ("score: " + score + "  scoreRun: " + scoreRun + "  chain: " + chain);
			break;
		}
	}
}
